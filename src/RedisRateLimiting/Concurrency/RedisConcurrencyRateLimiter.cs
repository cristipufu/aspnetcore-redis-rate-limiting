﻿using RedisRateLimiting.Concurrency;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace RedisRateLimiting
{
    public class RedisConcurrencyRateLimiter<TKey> : RateLimiter
    {
        private readonly RedisConcurrencyManager _redisManager;
        private readonly RedisConcurrencyRateLimiterOptions _options;
        private readonly ConcurrentQueue<Request> _queue = new();

        private readonly PeriodicTimer? _periodicTimer;

        private bool _disposed;

        private readonly ConcurrencyLease FailedLease = new(false, null, null);

        public override TimeSpan? IdleDuration => TimeSpan.Zero;

        public RedisConcurrencyRateLimiter(TKey partitionKey, RedisConcurrencyRateLimiterOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (options.PermitLimit <= 0)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than 0.", nameof(options.PermitLimit)), nameof(options));
            }
            if (options.QueueLimit < 0)
            {
                throw new ArgumentException(string.Format("{0} must be set to a value greater than 0.", nameof(options.QueueLimit)), nameof(options));
            }
            if (options.ConnectionMultiplexerFactory is null)
            {
                throw new ArgumentException(string.Format("{0} must not be null.", nameof(options.ConnectionMultiplexerFactory)), nameof(options));
            }

            _options = new RedisConcurrencyRateLimiterOptions
            {
                ConnectionMultiplexerFactory = options.ConnectionMultiplexerFactory,
                PermitLimit = options.PermitLimit,
                QueueLimit = options.QueueLimit,
                TryDequeuePeriod = options.TryDequeuePeriod,
            };

            _redisManager = new RedisConcurrencyManager(partitionKey?.ToString() ?? string.Empty, _options);

            if (_options.QueueLimit > 0)
            {
                _periodicTimer = new PeriodicTimer(_options.TryDequeuePeriod);

                _ = StartDequeueTimerAsync(_periodicTimer);
            }
        }

        public override RateLimiterStatistics? GetStatistics()
        {
            return _redisManager.GetStatistics();
        }

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            if (permitCount > _options.PermitLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.PermitLimit));
            }

            return AcquireAsyncCoreInternal(permitCount, cancellationToken);
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            if (permitCount > _options.PermitLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(permitCount), permitCount, string.Format("{0} permit(s) exceeds the permit limit of {1}.", permitCount, _options.PermitLimit));
            }

            var leaseContext = new ConcurencyLeaseContext
            {
                Limit = _options.PermitLimit,
                RequestId = Guid.NewGuid().ToString(),
                PermitCount = permitCount
            };

            var response = _redisManager.TryAcquireLease(leaseContext.RequestId, permitCount);

            leaseContext.Count = response.Count;

            if (response.Allowed)
            {
                return new ConcurrencyLease(isAcquired: true, this, leaseContext);
            }

            return new ConcurrencyLease(isAcquired: false, this, leaseContext);
        }

        private async ValueTask<RateLimitLease> AcquireAsyncCoreInternal(int permitCount, CancellationToken cancellationToken)
        {
            var leaseContext = new ConcurencyLeaseContext
            {
                Limit = _options.PermitLimit,
                RequestId = Guid.NewGuid().ToString(),
                PermitCount = permitCount
            };

            var response = await _redisManager.TryAcquireLeaseAsync(leaseContext.RequestId, permitCount, tryEnqueue: true);

            leaseContext.Count = response.Count;

            if (response.Allowed)
            {
                return new ConcurrencyLease(isAcquired: true, this, leaseContext);
            }

            if (response.Queued)
            {
                Request request = new()
                {
                    CancellationToken = cancellationToken,
                    LeaseContext = leaseContext,
                    TaskCompletionSource = new TaskCompletionSource<RateLimitLease>(),
                };

                if (cancellationToken.CanBeCanceled)
                {
                    request.CancellationTokenRegistration = cancellationToken.Register(static obj =>
                    {
                        // When the request gets canceled
                        var request = (Request)obj!;
                        request.TaskCompletionSource!.TrySetCanceled(request.CancellationToken);

                    }, request);
                }

                _queue.Enqueue(request);

                return await request.TaskCompletionSource.Task;
            }

            return new ConcurrencyLease(isAcquired: false, this, leaseContext);
        }

        private void Release(ConcurencyLeaseContext leaseContext)
        {
            if (leaseContext.RequestId is null) return;

            _redisManager.ReleaseLease(leaseContext.RequestId, leaseContext.PermitCount);
        }

        private async Task StartDequeueTimerAsync(PeriodicTimer periodicTimer)
        {
            while (await periodicTimer.WaitForNextTickAsync())
            {
                await TryDequeueRequestsAsync();
            }
        }

        private async Task TryDequeueRequestsAsync()
        {
            try
            {
                while (_queue.TryPeek(out var request))
                {
                    if (request.TaskCompletionSource!.Task.IsCompleted)
                    {
                        try
                        {
                            // The request was canceled while in the pending queue
                            await _redisManager.ReleaseQueueLeaseAsync(request.LeaseContext!.RequestId!, request.LeaseContext!.PermitCount);
                        }
                        finally
                        {
                            request.CancellationTokenRegistration.Dispose();

                            _queue.TryDequeue(out _);
                        }

                        continue;
                    }

                    var response = await _redisManager.TryAcquireLeaseAsync(request.LeaseContext!.RequestId!, request.LeaseContext!.PermitCount);

                    request.LeaseContext.Count = response.Count;

                    if (response.Allowed)
                    {
                        var pendingLease = new ConcurrencyLease(isAcquired: true, this, request.LeaseContext);

                        try
                        {
                            if (request.TaskCompletionSource?.TrySetResult(pendingLease) == false)
                            {
                                // The request was canceled after we acquired the lease
                                await _redisManager.ReleaseLeaseAsync(request.LeaseContext!.RequestId!, request.LeaseContext!.PermitCount);
                            }
                        }
                        finally
                        {
                            request.CancellationTokenRegistration.Dispose();

                            _queue.TryDequeue(out _);
                        }
                    }
                    else
                    {
                        // Try next time
                        break;
                    }
                }
            }
            catch
            {
                // 
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _periodicTimer?.Dispose();

            while (_queue.TryDequeue(out var request))
            {
                request?.CancellationTokenRegistration.Dispose();
                request?.TaskCompletionSource?.TrySetResult(FailedLease);
            }

            base.Dispose(disposing);
        }

        protected override ValueTask DisposeAsyncCore()
        {
            Dispose(true);

            return default;
        }

        private sealed class ConcurencyLeaseContext
        {
            public string? RequestId { get; set; }

            public long Count { get; set; }

            public long Limit { get; set; }

            public int PermitCount { get; set; }
        }

        private sealed class ConcurrencyLease : RateLimitLease
        {
            private static readonly string[] s_allMetadataNames = new[] { RateLimitMetadataName.Limit.Name, RateLimitMetadataName.Remaining.Name };

            private bool _disposed;
            private readonly RedisConcurrencyRateLimiter<TKey>? _limiter;
            private readonly ConcurencyLeaseContext? _context;

            public ConcurrencyLease(bool isAcquired, RedisConcurrencyRateLimiter<TKey>? limiter, ConcurencyLeaseContext? context)
            {
                IsAcquired = isAcquired;
                _limiter = limiter;
                _context = context;
            }

            public override bool IsAcquired { get; }

            public override IEnumerable<string> MetadataNames => s_allMetadataNames;

            public override bool TryGetMetadata(string metadataName, out object? metadata)
            {
                if (metadataName == RateLimitMetadataName.Limit.Name && _context is not null)
                {
                    metadata = _context.Limit.ToString();
                    return true;
                }

                if (metadataName == RateLimitMetadataName.Remaining.Name && _context is not null)
                {
                    metadata = _context.Limit - _context.Count;
                    return true;
                }

                metadata = default;
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (_context != null)
                {
                    _limiter?.Release(_context);
                }
            }
        }

        private sealed class Request
        {
            public CancellationToken CancellationToken { get; set; }

            public ConcurencyLeaseContext? LeaseContext { get; set; }

            public TaskCompletionSource<RateLimitLease>? TaskCompletionSource { get; set; }

            public CancellationTokenRegistration CancellationTokenRegistration { get; set; }
        }
    }
}
