using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Samhammer.ThreadPoolStarvationLogger.Test
{
    public class ServiceCollectionExtensionsTest
    {
        [Fact]
        public void AddThreadPoolStarvationLogger_Test_RegistersHostedService()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddThreadPoolStarvationLogger();

            // Assert
            // resolved via descriptor inspection instead of building the provider,
            // since resolving IHostedService would also require an ILogger<T> registration
            Assert.Contains(services, descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ThreadPoolStarvationLogger));
        }

        [Fact]
        public void AddThreadPoolStarvationLogger_Test_UsesDefaultOptions_WhenNotConfigured()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddThreadPoolStarvationLogger();
            var provider = services.BuildServiceProvider();

            // Assert
            var options = provider.GetRequiredService<IOptions<ThreadPoolStarvationLoggerOptions>>().Value;
            Assert.Equal(TimeSpan.FromMilliseconds(100), options.PollInterval);
            Assert.Equal(TimeSpan.FromMilliseconds(2000), options.WarnThreshold);
            Assert.Equal("/sys/fs/cgroup/cpu.stat", options.CgroupCpuStatPath);
        }

        [Fact]
        public void AddThreadPoolStarvationLogger_Test_AppliesConfigureAction()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddThreadPoolStarvationLogger(options =>
            {
                options.PollInterval = TimeSpan.FromSeconds(1);
                options.WarnThreshold = TimeSpan.FromSeconds(5);
                options.CgroupCpuStatPath = null;
            });
            var provider = services.BuildServiceProvider();

            // Assert
            var options = provider.GetRequiredService<IOptions<ThreadPoolStarvationLoggerOptions>>().Value;
            Assert.Equal(TimeSpan.FromSeconds(1), options.PollInterval);
            Assert.Equal(TimeSpan.FromSeconds(5), options.WarnThreshold);
            Assert.Null(options.CgroupCpuStatPath);
        }
    }
}
