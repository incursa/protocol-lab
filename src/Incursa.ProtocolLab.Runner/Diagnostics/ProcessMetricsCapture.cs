// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal static class ProcessMetricsCapture
{
    public static ProcessMetricSnapshot CaptureSnapshot(Process process)
    {
        try
        {
            process.Refresh();
        }
        catch
        {
            // Best effort only.
        }

        return new ProcessMetricSnapshot
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            WorkingSetBytes = TryGetLong(() => process.WorkingSet64),
            PrivateMemoryBytes = TryGetLong(() => process.PrivateMemorySize64),
            CpuTimeSeconds = TryGetDouble(() => process.TotalProcessorTime.TotalSeconds),
            ThreadCount = TryGetInt(() => process.Threads.Count),
            HandleCount = TryGetInt(() => process.HandleCount)
        };
    }

    public static async Task<IReadOnlyList<ProcessMetricSample>> SampleAsync(
        Process process,
        CancellationToken cancellationToken,
        TimeSpan interval)
    {
        var samples = new List<ProcessMetricSample>();
        var previous = CaptureSnapshot(process);

        while (!cancellationToken.IsCancellationRequested && !process.HasExited)
        {
            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested || process.HasExited)
            {
                break;
            }

            var current = CaptureSnapshot(process);
            samples.Add(new ProcessMetricSample
            {
                TimestampUtc = current.TimestampUtc,
                WorkingSetBytes = current.WorkingSetBytes,
                CpuTimeDeltaSeconds = current.CpuTimeSeconds.HasValue && previous.CpuTimeSeconds.HasValue
                    ? Math.Max(0d, current.CpuTimeSeconds.Value - previous.CpuTimeSeconds.Value)
                    : null,
                ThreadCount = current.ThreadCount,
                HandleCount = current.HandleCount
            });
            previous = current;
        }

        return samples;
    }

    public static TargetProcessMetrics BuildTargetProcessMetrics(
        TargetExecutionResult targetResult,
        ProcessMetricSnapshot? before,
        ProcessMetricSnapshot? after,
        IReadOnlyList<ProcessMetricSample> samples,
        DateTimeOffset benchmarkEndUtc)
    {
        var warnings = new List<string>();
        if (before?.WorkingSetBytes is null || after?.WorkingSetBytes is null)
        {
            warnings.Add("target-process-working-set-not-captured");
        }

        if (before?.CpuTimeSeconds is null || after?.CpuTimeSeconds is null)
        {
            warnings.Add("target-process-cpu-time-not-captured");
        }

        if (before is null || after is null)
        {
            warnings.Add("target-process-snapshot-unavailable");
        }

        if (samples.Count == 0)
        {
            warnings.Add("target-process-sampling-not-captured");
        }

        if (targetResult.ExitCode.HasValue && targetResult.ExitCode.Value != 0)
        {
            warnings.Add($"target-process-exit-code-{targetResult.ExitCode.Value}");
        }

        return new TargetProcessMetrics
        {
            ProcessId = targetResult.ProcessId,
            StartTimeUtc = targetResult.StartTimeUtc,
            ReadyTimeUtc = targetResult.ReadyTimeUtc,
            EndTimeUtc = benchmarkEndUtc,
            ExitCode = targetResult.ExitCode,
            Crashed = targetResult.ExitCode.HasValue && targetResult.ExitCode.Value != 0,
            Before = before,
            After = after,
            Samples = samples,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static long? TryGetLong(Func<long> capture)
    {
        try
        {
            return capture();
        }
        catch
        {
            return null;
        }
    }

    private static double? TryGetDouble(Func<double> capture)
    {
        try
        {
            return capture();
        }
        catch
        {
            return null;
        }
    }

    private static int? TryGetInt(Func<int> capture)
    {
        try
        {
            return capture();
        }
        catch
        {
            return null;
        }
    }
}
