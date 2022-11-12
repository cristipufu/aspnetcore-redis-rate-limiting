using Xunit;

namespace RedisRateLimiting.Tests
{
    public static class AssertExtensions
    {
        public static T Throws<T>(string expectedParamName, Func<object> testCode)
            where T : ArgumentException
        {
            T exception = Assert.Throws<T>(testCode);

            Assert.Equal(expectedParamName, exception.ParamName);

            return exception;
        }
    }
}
