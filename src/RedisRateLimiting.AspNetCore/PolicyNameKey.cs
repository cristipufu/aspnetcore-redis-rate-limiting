namespace RedisRateLimiting.AspNetCore
{
    internal sealed class PolicyNameKey
    {
        public required string PolicyName { get; init; }

        public override bool Equals(object? obj)
        {
            if (obj is PolicyNameKey key)
            {
                return PolicyName == key.PolicyName;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return PolicyName.GetHashCode();
        }

        public override string ToString()
        {
            return PolicyName;
        }
    }

}
