// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class ReportingTests
{
    [Fact]
    public void Builds_repetition_aware_run_report_and_markdown_summary()
    {
        var metadata = new RunMetadata(
            "bench-host",
            "Windows 11 Pro",
            ".NET 10.0.0",
            "X64",
            "X64",
            16,
            true,
            1_234_567_890,
            4_294_967_296,
            "Docker version 27.0.1, build deadbeef",
            "none",
            "overlay2|Docker Desktop");

        var first = CreateResult(1, 100, 20, 2_000, ["load generator saturation"], []);
        var second = CreateResult(2, 150, 10, 3_000, [], []);

        var report = RunReportBuilder.Build(
            "run-1",
            new DateTimeOffset(2026, 05, 26, 12, 34, 56, TimeSpan.Zero),
            metadata,
            [first, second]);

        Assert.Equal("run-1", report.RunId);
        Assert.Equal(ReportClaimLevel.Regression, report.ClaimLevel);
        Assert.Equal(2, report.Totals.ResultCount);
        Assert.Equal(1, report.Totals.AggregateCount);
        Assert.Equal(2, report.Totals.BenchmarkAttemptCount);
        Assert.Equal(2, report.Totals.ParsedMetricsCount);
        Assert.True(report.Totals.WarningCount >= 2);
        Assert.Equal(0, report.Totals.ErrorCount);
        Assert.Equal(1, report.Totals.SaturationWarningCount);
        Assert.Equal(2, report.Totals.Validation.Passed);
        Assert.Single(report.Aggregates);

        var aggregate = report.Aggregates[0];
        Assert.Equal(2, aggregate.Repetitions);
        Assert.Equal("oha", aggregate.LoadTool);
        Assert.Equal(TargetKinds.Process, aggregate.LoadToolMode);
        Assert.Equal(LoadToolCategories.ExternalReference, aggregate.LoadToolCategory);
        Assert.Equal(BenchmarkEvidenceClasses.ExternalReferenceLocal, aggregate.Evidence!.EvidenceClass);
        Assert.Equal(BenchmarkComparabilityStatuses.ComparableWithWarnings, aggregate.Evidence.ComparabilityStatus);
        Assert.Equal(2, aggregate.BenchmarkExecutionStatuses[LoadToolExecutionStatuses.Succeeded]);
        Assert.Equal(2, aggregate.Validation.Passed);
        Assert.Equal(125d, aggregate.RequestsPerSecond.Median);
        Assert.Equal(150d, aggregate.RequestsPerSecond.Best);
        Assert.Equal(100d, aggregate.RequestsPerSecond.Worst);
        Assert.Equal(15d, aggregate.LatencyMeanMs.Median);
        Assert.Equal(10d, aggregate.LatencyMeanMs.Best);
        Assert.Equal(20d, aggregate.LatencyMeanMs.Worst);
        Assert.Equal(2_500d, aggregate.ThroughputBytesPerSecond.Median);
        Assert.Equal(3_000d, aggregate.ThroughputBytesPerSecond.Best);
        Assert.Equal(2_000d, aggregate.ThroughputBytesPerSecond.Worst);
        Assert.Equal(1, aggregate.SaturationWarningCount);
        Assert.Contains("saturation", aggregate.SaturationWarnings[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("C:/temp/run/qlog", aggregate.QlogDirectory);
        Assert.Equal(2, aggregate.QlogAvailableCount);
        Assert.Equal(0, aggregate.TargetProcessMetricsMissingCount);
        Assert.Equal(2, aggregate.TargetProcessMetricsCapturedCount);
        Assert.Equal(2, aggregate.CountersCapturedCount);
        Assert.Equal(0, aggregate.CountersMissingCount);
        Assert.Equal(1234, aggregate.DiagnosticProcessId);
        Assert.Equal(DiagnosticTargetConfidenceLevels.High, aggregate.DiagnosticConfidence);
        Assert.Equal(2, aggregate.CountersCaptureStatuses[CounterCaptureStatuses.Succeeded]);

        var json = ResultJson.Serialize(report);
        var roundTrip = ResultJson.Deserialize<RunReport>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(report.RunId, roundTrip.RunId);
        Assert.Equal(report.Totals.AggregateCount, roundTrip.Totals.AggregateCount);
        Assert.Equal(report.Metadata!.HostName, roundTrip.Metadata!.HostName);
        Assert.Equal(report.ClaimLevel, roundTrip.ClaimLevel);

        var markdown = MarkdownSummaryWriter.Write(report);
        Assert.Contains("Claim level: regression", markdown);
        Assert.Contains("## Run Metadata", markdown);
        Assert.Contains("## Totals", markdown);
        Assert.Contains("## Aggregate Results", markdown);
        Assert.Contains("kestrel-http3", markdown);
        Assert.Contains("http.core.plaintext", markdown);
        Assert.Contains("oha", markdown);
        Assert.Contains("external-reference", markdown);
        Assert.Contains(BenchmarkEvidenceClasses.ExternalReferenceLocal, markdown);
        Assert.Contains(BenchmarkComparabilityStatuses.ComparableWithWarnings, markdown);
        Assert.Contains("evidence", markdown);
        Assert.Contains("comparability", markdown);
        Assert.Contains("succeeded 2", markdown);
        Assert.Contains("125 / 150 / 100", markdown);
        Assert.Contains("15 / 10 / 20", markdown);
        Assert.Contains("2500 / 3000 / 2000", markdown);
        Assert.Contains("saturation warnings", markdown);
        Assert.Contains("## Target Metadata", markdown);
        Assert.Contains("## Target Container Diagnostics", markdown);
        Assert.Contains("## Runtime Diagnostics", markdown);
        Assert.Contains("## Interpretation", markdown);
    }

    private static BenchmarkResult CreateResult(
        int repetition,
        double? requestsPerSecond,
        double? latencyMeanMs,
        double? throughputBytesPerSecond,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors)
    {
        var cell = new RunCell(
            new ImplementationManifest { Id = "kestrel-http3", Name = "Kestrel HTTP/3 Baseline" },
            new ScenarioDefinition { Id = "http.core.plaintext", Name = "HTTP Plaintext", Family = "http.application", Protocol = "h3", ImplementationRole = "server" },
            "h3",
            16,
            10,
            repetition,
            30,
            5,
            "clean");

        return BenchmarkResult.FromCell(
            "run-1",
            cell,
            new ScenarioValidationResult
            {
                ScenarioId = "http.core.plaintext",
                TargetId = "kestrel-http3",
                AdapterId = "",
                Protocol = "h3",
                Status = ValidationStatus.Passed,
                Summary = "ok"
            },
            "oha",
            true,
            new Dictionary<string, string>(),
            loadToolMode: TargetKinds.Process,
            loadToolCategory: LoadToolCategories.ExternalReference,
            loadToolVersion: "oha 1.14.0",
            benchmarkExecutionStatus: LoadToolExecutionStatuses.Succeeded,
            protocolProof: new ProtocolProofResult
            {
                Status = ValidationStatus.Passed,
                RequestedProtocol = "h3",
                ProvenProtocol = "h3",
                Method = "validation"
            },
            targetExecution: new TargetExecutionResult
            {
                Status = TargetExecutionStatuses.Ready,
                Started = true,
                Ready = true,
                StartTimeUtc = new DateTimeOffset(2026, 05, 26, 12, 0, 0, TimeSpan.Zero),
                ReadyTimeUtc = new DateTimeOffset(2026, 05, 26, 12, 0, 1, TimeSpan.Zero),
                ProcessId = 1234,
                CommandLine = "dotnet run --project servers/KestrelBenchServer"
            })
        with
        {
            Metrics = new HttpMetrics
            {
                RequestsPerSecond = requestsPerSecond,
                LatencyMeanMs = latencyMeanMs,
                ThroughputBytesPerSecond = throughputBytesPerSecond
            },
            TargetProcessMetrics = new TargetProcessMetrics
            {
                ProcessId = 1234,
                StartTimeUtc = new DateTimeOffset(2026, 05, 26, 12, 0, 0, TimeSpan.Zero),
                ReadyTimeUtc = new DateTimeOffset(2026, 05, 26, 12, 0, 1, TimeSpan.Zero),
                EndTimeUtc = new DateTimeOffset(2026, 05, 26, 12, 0, 12, TimeSpan.Zero),
                ExitCode = 0,
                Crashed = false,
                Before = new ProcessMetricSnapshot
                {
                    TimestampUtc = new DateTimeOffset(2026, 05, 26, 12, 0, 1, TimeSpan.Zero),
                    WorkingSetBytes = 100,
                    PrivateMemoryBytes = 200,
                    CpuTimeSeconds = 1.0,
                    ThreadCount = 4,
                    HandleCount = 20
                },
                After = new ProcessMetricSnapshot
                {
                    TimestampUtc = new DateTimeOffset(2026, 05, 26, 12, 0, 12, TimeSpan.Zero),
                    WorkingSetBytes = 200,
                    PrivateMemoryBytes = 300,
                    CpuTimeSeconds = 2.0,
                    ThreadCount = 5,
                    HandleCount = 21
                },
                Samples =
                [
                    new ProcessMetricSample
                    {
                        TimestampUtc = new DateTimeOffset(2026, 05, 26, 12, 0, 2, TimeSpan.Zero),
                        WorkingSetBytes = 150,
                        CpuTimeDeltaSeconds = 0.25,
                        ThreadCount = 4,
                        HandleCount = 20
                    }
                ]
            },
            DiagnosticTarget = new DiagnosticTarget
            {
                RootProcessId = 1234,
                ResolvedProcessId = 1234,
                ResolvedProcessName = "dotnet",
                ResolutionStrategy = DiagnosticTargetResolutionStrategies.RootProcess,
                CommandLine = "dotnet exec servers/KestrelBenchServer/bin/Debug/net10.0/KestrelBenchServer.dll",
                ExecutablePath = "dotnet",
                WorkingDirectory = TestPaths.RepoRoot,
                Confidence = DiagnosticTargetConfidenceLevels.High
            },
            CountersAvailable = true,
            CountersCaptureStatus = CounterCaptureStatuses.Succeeded,
            CountersSummary = new RuntimeCounterSummary
            {
                Samples = 2,
                CpuMean = 50 + repetition,
                CpuMax = 75 + repetition,
                AllocationRateMean = 1_000 + repetition,
                Gen0CollectionsDelta = repetition,
                Gen1CollectionsDelta = 0,
                Gen2CollectionsDelta = 0,
                ThreadPoolQueueLengthMax = 1,
                ExceptionRateMean = 0
            },
            QlogDirectory = "C:/temp/run/qlog",
            QlogFileCount = 2,
            Evidence = new BenchmarkEvidenceAssessment
            {
                EvidenceClass = BenchmarkEvidenceClasses.ExternalReferenceLocal,
                EvidenceReasons = [BenchmarkEvidenceReasons.ExternalReferenceLoadToolProven],
                ComparabilityStatus = BenchmarkComparabilityStatuses.ComparableWithWarnings,
                ComparabilityWarnings = [BenchmarkEvidenceReasons.NoRepeatedStableMedian]
            },
            Warnings = warnings.ToList(),
            Errors = errors.ToList()
        };
    }
}
