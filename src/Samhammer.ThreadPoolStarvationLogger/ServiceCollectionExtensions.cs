using System;
using Microsoft.Extensions.DependencyInjection;

namespace Samhammer.ThreadPoolStarvationLogger
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddThreadPoolStarvationLogger(this IServiceCollection services, Action<ThreadPoolStarvationLoggerOptions> configure = null)
        {
            services.Configure<ThreadPoolStarvationLoggerOptions>(options =>
            {
                configure?.Invoke(options);
            });

            return services.AddHostedService<ThreadPoolStarvationLogger>();
        }
    }
}
