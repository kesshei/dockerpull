using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DockerPull
{
    public static class RetryHelper
    {
        public static T Retry<T>(Func<T> operation, int maxRetries = 5)
        {
            int retryCount = 0;
            while (true)
            {
                try
                {
                    return operation();
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount > maxRetries)
                    {
                        return default;
                    }
                }
            }
        }
        public static async Task<T> RetryAsync<T>(Func<Task<T>> operation, int maxRetries = 5)
        {
            int retryCount = 0;
            while (true)
            {
                try
                {
                    // 执行传入的异步操作
                    return await operation();
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount > maxRetries)
                    {
                        return default;
                    }
                }
            }
        }
    }
}
