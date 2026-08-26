using System;

namespace Samhammer.ThreadPoolStarvationLogger
{
    public class ThreadPoolStarvationLoggerOptions
    {
        /// <summary>
        /// How often a canary work item is queued to the ThreadPool to measure dispatch latency.
        /// </summary>
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// A dispatch delay above this threshold is logged as a warning.
        /// </summary>
        public TimeSpan WarnThreshold { get; set; } = TimeSpan.FromMilliseconds(2000);

        /// <summary>
        /// Path to the cgroup v2 cpu.stat file used to correlate a dispatch delay with CPU throttling.
        /// Set to null to disable the correlation (e.g. on Windows or cgroup v1 hosts).
        /// </summary>
        public string CgroupCpuStatPath { get; set; } = "/sys/fs/cgroup/cpu.stat";
    }
}
