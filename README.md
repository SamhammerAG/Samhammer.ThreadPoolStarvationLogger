# Samhammer.ThreadPoolStarvationLogger

Detects .NET `ThreadPool` starvation in a running application and logs the details needed to actually diagnose it, instead of just a vague symptom.

## Why

.NET's `ThreadPool` starts with a minimum thread count derived from `Environment.ProcessorCount` — which, in a container with a CPU *limit* (e.g. Kubernetes `resources.limits.cpu: "1"`), can be as low as `1`. When demand briefly exceeds that, the pool grows slowly by design, and any code waiting for a free thread (including framework internals like ASP.NET Core's health check middleware) can stall for seconds. This typically shows up as sporadic, hard-to-reproduce timeouts or `TaskCanceledException`s with no obvious cause.

This library queues a trivial no-op work item to the `ThreadPool` on a fixed interval and measures how long it actually took to get dispatched. When that delay exceeds a configurable threshold, it logs a detailed snapshot to help you tell starvation apart from other causes:

- `ThreadCount` before/after — did the pool grow during the stall, or was it already sitting flat?
- `PendingWorkItems` before/after — was there a backlog, or did the queue start empty and pile up?
- `AvailableWorker` / `AvailableIocp` vs. their configured min/max — how much headroom the pool thinks it has.
- `CpuThrottledMs` — on cgroup v2 hosts (e.g. Kubernetes on Linux), the CPU time lost to CFS quota throttling *during that exact delay window*, read directly from `/sys/fs/cgroup/cpu.stat`. This lets you tell ThreadPool starvation apart from the container simply not getting CPU time at all — two problems that look identical from the outside but need different fixes.

## Install

```
dotnet add package Samhammer.ThreadPoolStarvationLogger
```

## Usage

```csharp
using Samhammer.ThreadPoolStarvationLogger;

builder.Services.AddThreadPoolStarvationLogger();
```

With custom options:

```csharp
builder.Services.AddThreadPoolStarvationLogger(options =>
{
    options.PollInterval = TimeSpan.FromMilliseconds(200);
    options.WarnThreshold = TimeSpan.FromSeconds(1);
});
```

Or via configuration binding, like any other options class:

```csharp
builder.Services.Configure<ThreadPoolStarvationLoggerOptions>(builder.Configuration.GetSection("ThreadPoolStarvationLogger"));
builder.Services.AddThreadPoolStarvationLogger();
```

## Options

| Option | Default | Description |
| --- | --- | --- |
| `PollInterval` | `100ms` | How often a canary work item is queued to measure dispatch latency. |
| `WarnThreshold` | `2000ms` | A dispatch delay above this is logged as a warning. Lower this to catch smaller stalls, raise it to reduce log volume. |
| `CgroupCpuStatPath` | `/sys/fs/cgroup/cpu.stat` | Path to the cgroup v2 `cpu.stat` file used for the `CpuThrottledMs` correlation. Set to `null` to disable it. On hosts where the file doesn't exist (Windows, cgroup v1, bare metal), the correlation is skipped automatically and `CpuThrottledMs` is logged as `n/a` — no exception, no crash. |

## Example log output

```
ThreadPool dispatch delay of 4039ms detected. CpuThrottledMs=0.4 ThreadCount 7->7 PendingWorkItems 0->23 AvailableWorker=32762/32767 (min 1) AvailableIocp=1000/1000 (min 1)
```

Reading it: the queue was empty right before the delay (`PendingWorkItems 0->23`), the thread count never grew (`ThreadCount 7->7`), and almost none of the 4 seconds was CPU throttling (`CpuThrottledMs=0.4`) — pointing at ThreadPool scheduling itself, not the container's CPU allocation, as the cause.

## What this does not fix

This is a diagnostic tool, not a mitigation. If it confirms starvation, the standard fix is raising the ThreadPool's minimum thread count above the CPU-limit-derived default, e.g.:

```csharp
ThreadPool.SetMinThreads(20, 20);
```

set as early as possible during application startup. See the [.NET docs on `ThreadPool.SetMinThreads`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.threadpool.setminthreads) for the tradeoffs involved.

## License

[MIT](LICENSE)

## Contribute

#### How to publish package
- create git tag
- The nuget package will be published automatically by a github action
