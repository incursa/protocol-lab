// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using Incursa.ProtocolLab.Runner;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class RuntimeDiagnosticsTests
{
    [Fact]
    public void Resolves_direct_exec_root_process_with_high_confidence()
    {
        using var process = Process.GetCurrentProcess();
        var result = new TargetExecutionResult
        {
            Status = TargetExecutionStatuses.Ready,
            Started = true,
            Ready = true,
            ProcessId = process.Id,
            CommandLine = "dotnet exec KestrelBenchServer.dll",
            ExecutablePath = "dotnet",
            WorkingDirectory = TestPaths.RepoRoot
        };

        var target = DiagnosticTargetResolver.Resolve(process, result);

        Assert.Equal(process.Id, target.RootProcessId);
        Assert.Equal(process.Id, target.ResolvedProcessId);
        Assert.Equal(DiagnosticTargetResolutionStrategies.RootProcess, target.ResolutionStrategy);
        Assert.Equal(DiagnosticTargetConfidenceLevels.High, target.Confidence);
    }

    [Fact]
    public void Dotnet_run_wrapper_is_not_treated_as_resolved_server_process()
    {
        using var process = Process.GetCurrentProcess();
        var result = new TargetExecutionResult
        {
            Status = TargetExecutionStatuses.Ready,
            Started = true,
            Ready = true,
            ProcessId = process.Id,
            CommandLine = "dotnet run --project servers/KestrelBenchServer/KestrelBenchServer.csproj",
            ExecutablePath = "dotnet",
            WorkingDirectory = TestPaths.RepoRoot
        };

        var target = DiagnosticTargetResolver.Resolve(process, result);

        Assert.Null(target.ResolvedProcessId);
        Assert.Equal(DiagnosticTargetResolutionStrategies.Unresolved, target.ResolutionStrategy);
        Assert.Equal(DiagnosticTargetConfidenceLevels.Low, target.Confidence);
        Assert.Contains(target.Warnings, warning => warning.Contains("wrapper", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Missing_dotnet_counters_reports_unavailable()
    {
        var status = await RuntimeCounterSession.DetectToolAsync("definitely-not-dotnet-counters-phase2k");

        Assert.False(status.Available);
        Assert.NotEmpty(status.Warnings);
    }

    [Fact]
    public void Parses_counter_json_summary_best_effort()
    {
        var directory = Directory.CreateTempSubdirectory("protocol-lab-counters-");
        try
        {
            var path = Path.Combine(directory.FullName, "counters.raw.json");
            File.WriteAllText(path, """
            [
              { "name": "cpu-usage", "mean": 25.0 },
              { "name": "cpu-usage", "mean": 75.0 },
              { "name": "alloc-rate", "mean": 1024.0 },
              { "name": "gen-0-gc-count", "value": 1 },
              { "name": "gen-0-gc-count", "value": 3 },
              { "name": "threadpool-queue-length", "value": 4 },
              { "name": "exception-count", "value": 0 }
            ]
            """);

            var summary = RuntimeCounterParser.Parse(
                path,
                new DateTimeOffset(2026, 05, 27, 1, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 05, 27, 1, 0, 10, TimeSpan.Zero));

            Assert.Equal(7, summary.Samples);
            Assert.Equal(50, summary.CpuMean);
            Assert.Equal(75, summary.CpuMax);
            Assert.Equal(1024, summary.AllocationRateMean);
            Assert.Equal(2, summary.Gen0CollectionsDelta);
            Assert.Equal(4, summary.ThreadPoolQueueLengthMax);
            Assert.Equal(0, summary.ExceptionRateMean);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Parses_counter_csv_summary_best_effort()
    {
        var directory = Directory.CreateTempSubdirectory("protocol-lab-counters-");
        try
        {
            var path = Path.Combine(directory.FullName, "counters.raw.csv");
            File.WriteAllText(path, """
            Counter Name,Mean
            cpu-usage,10
            cpu-usage,20
            alloc-rate,2048
            threadpool-queue-length,5
            """);

            var summary = RuntimeCounterParser.Parse(
                path,
                new DateTimeOffset(2026, 05, 27, 1, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 05, 27, 1, 0, 10, TimeSpan.Zero));

            Assert.Equal(4, summary.Samples);
            Assert.Equal(15, summary.CpuMean);
            Assert.Equal(20, summary.CpuMax);
            Assert.Equal(2048, summary.AllocationRateMean);
            Assert.Equal(5, summary.ThreadPoolQueueLengthMax);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
