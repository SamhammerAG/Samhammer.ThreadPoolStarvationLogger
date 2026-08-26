using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Samhammer.ThreadPoolStarvationLogger
{
    public class ThreadPoolStarvationLogger : BackgroundService
    {
        private ILogger<ThreadPoolStarvationLogger> Logger { get; }

        private ThreadPoolStarvationLoggerOptions Options { get; }

        public ThreadPoolStarvationLogger(ILogger<ThreadPoolStarvationLogger> logger, IOptions<ThreadPoolStarvationLoggerOptions> options)
        {
            Logger = logger;
            Options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var pendingWorkItemsBefore = ThreadPool.PendingWorkItemCount;
                    var threadCountBefore = ThreadPool.ThreadCount;
                    var throttledUsecBefore = TryReadCgroupThrottledUsec();

                    var stopwatch = Stopwatch.StartNew();
                    await Task.Run(() => { }, stoppingToken).ConfigureAwait(false);
                    stopwatch.Stop();

                    if (stopwatch.Elapsed > Options.WarnThreshold)
                    {
                        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableCompletionPortThreads);
                        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
                        ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);

                        var throttledUsecAfter = TryReadCgroupThrottledUsec();
                        var cpuThrottledDeltaMs = throttledUsecBefore.HasValue && throttledUsecAfter.HasValue
                            ? ((throttledUsecAfter.Value - throttledUsecBefore.Value) / 1000.0).ToString("F1", CultureInfo.InvariantCulture)
                            : "n/a";

                        Logger.LogWarning(
                            "ThreadPool dispatch delay of {DelayMs}ms detected. CpuThrottledMs={CpuThrottledDeltaMs} ThreadCount {ThreadCountBefore}->{ThreadCountAfter} PendingWorkItems {PendingWorkItemsBefore}->{PendingWorkItemsAfter} AvailableWorker={AvailableWorkerThreads}/{MaxWorkerThreads} (min {MinWorkerThreads}) AvailableIocp={AvailableCompletionPortThreads}/{MaxCompletionPortThreads} (min {MinCompletionPortThreads})",
                            stopwatch.ElapsedMilliseconds,
                            cpuThrottledDeltaMs,
                            threadCountBefore,
                            ThreadPool.ThreadCount,
                            pendingWorkItemsBefore,
                            ThreadPool.PendingWorkItemCount,
                            availableWorkerThreads,
                            maxWorkerThreads,
                            minWorkerThreads,
                            availableCompletionPortThreads,
                            maxCompletionPortThreads,
                            minCompletionPortThreads);
                    }

                    await Task.Delay(Options.PollInterval, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // expected when the host is shutting down
            }
        }

        private long? TryReadCgroupThrottledUsec()
        {
            if (string.IsNullOrEmpty(Options.CgroupCpuStatPath) || !File.Exists(Options.CgroupCpuStatPath))
            {
                return null;
            }

            try
            {
                foreach (var line in File.ReadLines(Options.CgroupCpuStatPath))
                {
                    if (line.StartsWith("throttled_usec ", StringComparison.Ordinal)
                        && long.TryParse(line.AsSpan("throttled_usec ".Length), CultureInfo.InvariantCulture, out var throttledUsec))
                    {
                        return throttledUsec;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }
    }
}
