// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class EvidenceReportTests
{
    [Fact]
    public void Builds_evidence_report_with_full_data()
    {
        var metadata = new RunMetadata(
            "bench-host",
            "Windows 11 Pro",
            ".NET 10.0.0",
            "X64",
            "X64",
            16,
            true,
            1_000_000_000,
            16_000_000_000,
            "Docker version 27.0.1",
            "none",
            "overlay2",
            TimestampUtc: new DateTimeOffset(2026, 05, 30, 12, 0, 0, TimeSpan.Zero),
            GitCommit: "abc123def",
            WorkingTreeStatus: "clean");

        var cells = new List<RunCell>
        {
            CreateCell("kestrel-http3", "Kestrel HTTP/3", "http.core.plaintext", "HTTP Plaintext", "h3", "http.application", 16, 10, 1),
            CreateCell("incursa-http3", "Incursa HTTP/3", "http.core.plaintext", "HTTP Plaintext", "h3", "http.application", 16, 10, 1),
            CreateCell("kestrel-http3", "Kestrel HTTP/3", "http.core.json", "HTTP JSON", "h3", "http.application", 16, 10, 1),
            CreateCell("incursa-http3", "Incursa HTTP/3", "http.core.json", "HTTP JSON", "h3", "http.application", 16, 10, 1),
            CreateCell("msquic-dotnet", "MsQuic .NET", "quic.transport.echo", "QUIC Echo", "quic", "quic.transport", 1, 1, 1),
        };

        var compatibilities = new List<RunCellCompatibility>
        {
            RunCellCompatibility.Supported(),
            RunCellCompatibility.Supported(),
            RunCellCompatibility.Supported(),
            RunCellCompatibility.Supported(),
            RunCellCompatibility.MissingLoadTool("No load tool supports quic echo"),
        };

        var results = new List<BenchmarkResult>
        {
            CreateAcceptedResult("kestrel-http3", "Kestrel HTTP/3", "http.core.plaintext", "HTTP Plaintext", "h3", 16, 10, 1, 5000, 2.5, 3.0, 4.0, 2.8, 100_000),
            CreateAcceptedResult("incursa-http3", "Incursa HTTP/3", "http.core.plaintext", "HTTP Plaintext", "h3", 16, 10, 1, 4800, 2.6, 3.2, 4.1, 3.0, 95_000),
            CreateAcceptedResult("kestrel-http3", "Kestrel HTTP/3", "http.core.json", "HTTP JSON", "h3", 16, 10, 1, 4500, 3.0, 3.5, 4.5, 3.2, 90_000),
            CreateAcceptedResult("incursa-http3", "Incursa HTTP/3", "http.core.json", "HTTP JSON", "h3", 16, 10, 1, 4300, 3.2, 3.8, 4.8, 3.5, 85_000),
            CreateFailedResult("kestrel-http3", "Kestrel HTTP/3", "http.core.status", "HTTP Status", "h3", 16, 10, 1),
            CreateUnsupportedResult("msquic-dotnet", "MsQuic .NET", "quic.transport.echo", "QUIC Echo", "quic", 1, 1, 1),
            CreateLoadToolFailedResult("incursa-http3", "Incursa HTTP/3", "http.payload.bytes-1mb", "1MB Payload", "h3", 16, 10, 1),
            CreateParserFailedResult("kestrel-http3", "Kestrel HTTP/3", "http.payload.bytes-64kb", "64KB Payload", "h3", 16, 10, 1),
        };

        var report = EvidenceReportBuilder.Build(
            "test-run",
            new DateTimeOffset(2026, 05, 30, 12, 0, 0, TimeSpan.Zero),
            metadata,
            "h3-local-v1",
            "H3 Local Comparison v1",
            cells,
            compatibilities,
            results);

        Assert.Equal("test-run", report.RunId);
        Assert.NotNull(report.Identity);
        Assert.Equal("h3-local-v1", report.Identity.SuiteId);
        Assert.Equal("H3 Local Comparison v1", report.Identity.SuiteTitle);
        Assert.Equal("abc123def", report.Identity.GitCommit);
        Assert.Single(report.Identity.Warnings);

        Assert.Equal(5, report.MatrixSummary.TotalCells);
        Assert.Equal(4, report.MatrixSummary.SupportedCells);
        Assert.Equal(1, report.MatrixSummary.UnsupportedCells);
        Assert.Equal(1, report.MatrixSummary.MissingLoadTool);

        Assert.Equal(6, report.ValidationSummary.Passed);
        Assert.Equal(1, report.ValidationSummary.Failed);
        Assert.Equal(1, report.ValidationSummary.Unsupported);
        Assert.Equal(0, report.ValidationSummary.InfrastructureFailure);

        Assert.Equal(4, report.BenchmarkAcceptance.AcceptedBenchmarks);
        Assert.Equal(0, report.BenchmarkAcceptance.RejectedBenchmarks);
        Assert.Equal(1, report.BenchmarkAcceptance.NotRunValidationFailed);
        Assert.Equal(1, report.BenchmarkAcceptance.NotRunUnsupported);
        Assert.Equal(1, report.BenchmarkAcceptance.NotRunLoadToolFailed);
        Assert.Equal(1, report.BenchmarkAcceptance.NotRunParserFailed);

        Assert.Equal(2, report.ComparisonGroups.Count);
        Assert.All(report.ComparisonGroups, g => Assert.Equal(2, g.Entries.Count));

        Assert.NotEmpty(report.Warnings);
        Assert.NotEmpty(report.ArtifactIndex);
    }

    [Fact]
    public void Matrix_summary_counts_are_correct()
    {
        var cells = new List<RunCell>
        {
            CreateCell("kestrel-http3", "Kestrel", "scenario1", "S1", "h3", "http", 1, 1, 1),
            CreateCell("kestrel-http3", "Kestrel", "scenario2", "S2", "h3", "http", 1, 1, 1),
            CreateCell("incursa-http3", "Incursa", "scenario1", "S1", "h3", "http", 1, 1, 1),
            CreateCell("incursa-http3", "Incursa", "scenario2", "S2", "h3", "http", 1, 1, 1),
            CreateCell("msquic", "MsQuic", "scenario1", "S1", "h3", "http", 1, 1, 1),
        };

        var compatibilities = new List<RunCellCompatibility>
        {
            RunCellCompatibility.Supported(),
            RunCellCompatibility.Supported(),
            RunCellCompatibility.MissingCapability("protocol 'h3' is not supported"),
            RunCellCompatibility.MissingLoadTool("No load tool for http"),
            RunCellCompatibility.PlaceholderNotRunnable("Placeholder"),
        };

        var results = new List<BenchmarkResult>
        {
            CreateAcceptedResult("kestrel-http3", "Kestrel", "scenario1", "S1", "h3", 1, 1, 1, 100, 1, 2, 3, 1.5, 1000),
            CreateAcceptedResult("kestrel-http3", "Kestrel", "scenario2", "S2", "h3", 1, 1, 1, 100, 1, 2, 3, 1.5, 1000),
        };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, cells, compatibilities, results);

        Assert.Equal(5, report.MatrixSummary.TotalCells);
        Assert.Equal(2, report.MatrixSummary.SupportedCells);
        Assert.Equal(3, report.MatrixSummary.UnsupportedCells);
        Assert.Equal(1, report.MatrixSummary.MissingCapability);
        Assert.Equal(1, report.MatrixSummary.MissingLoadTool);
        Assert.Equal(1, report.MatrixSummary.PlaceholderNotRunnable);
        Assert.Equal(0, report.MatrixSummary.OtherUnsupported);
        Assert.Equal(3, report.MatrixSummary.UnsupportedCellDetails.Count);
    }

    [Fact]
    public void Matrix_summary_handles_experimental_and_incompatible_cells()
    {
        var cells = new List<RunCell>
        {
            CreateCell("kestrel-http3", "Kestrel", "exp1", "Experimental 1", "h3", "http", 1, 1, 1),
            CreateCell("kestrel-http3", "Kestrel", "bad-shape", "Bad Shape", "h3", "http", 1, 1, 1),
            CreateCell("kestrel-http3", "Kestrel", "bad-profile", "Bad Profile", "h3", "http", 1, 1, 1),
            CreateCell("kestrel-http3", "Kestrel", "good", "Good", "h3", "http", 1, 1, 1),
        };

        var compatibilities = new List<RunCellCompatibility>
        {
            RunCellCompatibility.ExperimentalNotEnabled("Scenario is experimental"),
            RunCellCompatibility.IncompatibleTrafficShape("No load tool for shape"),
            RunCellCompatibility.IncompatibleLoadProfile("Bad load profile"),
            RunCellCompatibility.Supported(),
        };

        var results = new List<BenchmarkResult>
        {
            CreateAcceptedResult("kestrel-http3", "Kestrel", "good", "Good", "h3", 1, 1, 1, 100, 1, 2, 3, 1.5, 1000),
        };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, cells, compatibilities, results);

        Assert.Equal(4, report.MatrixSummary.TotalCells);
        Assert.Equal(1, report.MatrixSummary.SupportedCells);
        Assert.Equal(3, report.MatrixSummary.UnsupportedCells);
        Assert.Equal(1, report.MatrixSummary.ExperimentalDisabled);
        Assert.Equal(1, report.MatrixSummary.IncompatibleTrafficShape);
        Assert.Equal(1, report.MatrixSummary.IncompatibleLoadProfile);
    }

    [Fact]
    public void Validation_summary_counts_all_statuses()
    {
        var results = new List<BenchmarkResult>
        {
            CreateResult("impl", "scenario", "h3", 1, 1, 1, ValidationStatus.Passed, LoadToolExecutionStatuses.Succeeded, true),
            CreateResult("impl", "scenario", "h3", 1, 1, 2, ValidationStatus.Passed, LoadToolExecutionStatuses.Succeeded, true),
            CreateResult("impl", "scenario", "h3", 1, 1, 3, ValidationStatus.Failed, LoadToolExecutionStatuses.Skipped, false),
            CreateResult("impl", "scenario", "h3", 1, 1, 4, ValidationStatus.Unsupported, LoadToolExecutionStatuses.Skipped, false),
            CreateResult("impl", "scenario", "h3", 1, 1, 5, ValidationStatus.NotApplicable, LoadToolExecutionStatuses.Skipped, false),
            CreateResult("impl", "scenario", "h3", 1, 1, 6, ValidationStatus.Inconclusive, LoadToolExecutionStatuses.Skipped, false),
            CreateResult("impl", "scenario", "h3", 1, 1, 7, ValidationStatus.InfrastructureFailure, LoadToolExecutionStatuses.Skipped, false),
        };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, [], [], results);

        Assert.Equal(2, report.ValidationSummary.Passed);
        Assert.Equal(1, report.ValidationSummary.Failed);
        Assert.Equal(1, report.ValidationSummary.Unsupported);
        Assert.Equal(1, report.ValidationSummary.NotApplicable);
        Assert.Equal(1, report.ValidationSummary.Inconclusive);
        Assert.Equal(1, report.ValidationSummary.InfrastructureFailure);
    }

    [Fact]
    public void Benchmark_acceptance_classifies_correctly()
    {
        var results = new List<BenchmarkResult>
        {
            CreateResult("impl1", "scenario1", "h3", 1, 1, 1, ValidationStatus.Passed, LoadToolExecutionStatuses.Succeeded, true),
            CreateResult("impl1", "scenario2", "h3", 1, 1, 1, ValidationStatus.Passed, LoadToolExecutionStatuses.Succeeded, true),
            CreateResult("impl1", "scenario3", "h3", 1, 1, 1, ValidationStatus.Failed, LoadToolExecutionStatuses.Skipped, false),
            CreateResult("impl1", "scenario4", "h3", 1, 1, 1, ValidationStatus.Passed, LoadToolExecutionStatuses.Skipped, false),
            CreateResult("impl1", "scenario5", "h3", 1, 1, 1, ValidationStatus.Passed, LoadToolExecutionStatuses.Failed, false),
            CreateResult("impl1", "scenario6", "h3", 1, 1, 1, ValidationStatus.Passed, LoadToolExecutionStatuses.Succeeded, false),
        };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, [], [], results);

        Assert.Equal(2, report.BenchmarkAcceptance.AcceptedBenchmarks);
        Assert.Equal(0, report.BenchmarkAcceptance.RejectedBenchmarks);
        Assert.Equal(1, report.BenchmarkAcceptance.NotRunValidationFailed);
        Assert.Equal(1, report.BenchmarkAcceptance.NotRunUnsupported);
        Assert.Equal(1, report.BenchmarkAcceptance.NotRunLoadToolFailed);
        Assert.Equal(1, report.BenchmarkAcceptance.NotRunParserFailed);
        Assert.Equal(2, report.BenchmarkAcceptance.AcceptedDetails.Count);
        Assert.Equal(4, report.BenchmarkAcceptance.NotRunDetails.Count);
    }

    [Fact]
    public void Comparison_groups_only_include_comparable_pairs()
    {
        var results = new List<BenchmarkResult>
        {
            CreateAcceptedResult("kestrel", "Kestrel", "scenario1", "S1", "h3", 16, 10, 1, 5000, 2, 3, 4, 2.5, 100000),
            CreateAcceptedResult("incursa", "Incursa", "scenario1", "S1", "h3", 16, 10, 1, 4800, 2.1, 3.1, 4.1, 2.6, 95000),
            CreateAcceptedResult("kestrel", "Kestrel", "scenario1", "S1", "h3", 16, 10, 2, 5100, 1.9, 2.9, 3.9, 2.4, 102000),
            CreateAcceptedResult("kestrel", "Kestrel", "scenario2", "S2", "h3", 16, 10, 1, 100, 1, 2, 3, 1.5, 1000),
        };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, [], [], results);

        Assert.Single(report.ComparisonGroups);
        var group = report.ComparisonGroups[0];
        Assert.Equal("scenario1", group.ScenarioId);
        Assert.Equal(2, group.Entries.Count);

        var kestrelEntry = group.Entries.First(e => e.ImplementationId == "kestrel");
        Assert.Equal(2, kestrelEntry.Repetitions);
        Assert.NotNull(kestrelEntry.RequestsPerSecondMedian);

        var incursaEntry = group.Entries.First(e => e.ImplementationId == "incursa");
        Assert.Equal(1, incursaEntry.Repetitions);
    }

    [Fact]
    public void Comparison_groups_require_at_least_two_implementations()
    {
        var results = new List<BenchmarkResult>
        {
            CreateAcceptedResult("kestrel", "Kestrel", "scenario1", "S1", "h3", 16, 10, 1, 5000, 2, 3, 4, 2.5, 100000),
            CreateAcceptedResult("kestrel", "Kestrel", "scenario1", "S1", "h3", 16, 10, 2, 5100, 1.9, 2.9, 3.9, 2.4, 102000),
        };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, [], [], results);

        Assert.Empty(report.ComparisonGroups);
    }

    [Fact]
    public void Evidence_warnings_are_generated()
    {
        var cells = new List<RunCell>
        {
            CreateCell("kestrel-http3", "Kestrel", "scenario1", "S1", "h3", "http", 1, 1, 1),
            CreateCell("ns-http3", "ns", "scenario1", "S1", "h3", "http", 1, 1, 1),
        };

        var compatibilities = new List<RunCellCompatibility>
        {
            RunCellCompatibility.Supported(),
            RunCellCompatibility.MissingLoadTool("No load tool"),
        };

        var results = new List<BenchmarkResult>
        {
            CreateAcceptedResult("kestrel-http3", "Kestrel", "scenario1", "S1", "h3", 1, 1, 1, 100, 1, 2, 3, 1.5, 1000),
        };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, cells, compatibilities, results);

        Assert.Contains(report.Warnings, w => w.Category == "environment");
        Assert.Contains(report.Warnings, w => w.Category == "matrix" && w.Message.Contains("unsupported"));
    }

    [Fact]
    public void Evidence_warnings_include_docker_bridge_warning()
    {
        var result = CreateAcceptedResultBase("kestrel-http3", "Kestrel", "scenario1", "S1", "h3", 1, 1, 1, 100, 1, 2, 3, 1.5, 1000);
        result = result with { TargetDockerNetworkMode = TargetNetworkModes.PublishedPort };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, [], [], [result]);

        Assert.Contains(report.Warnings, w => w.Category == "network" && w.Message.Contains("Docker bridge"));
    }

    [Fact]
    public void Evidence_warnings_include_parser_fallback_warning()
    {
        var result = CreateResult("impl", "scenario", "h3", 1, 1, 1,
            ValidationStatus.Passed, LoadToolExecutionStatuses.Succeeded, false);

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, [], [], [result]);

        Assert.Contains(report.Warnings, w => w.Category == "parser" && w.Message.Contains("Parser fallback"));
    }

    [Fact]
    public void Evidence_warnings_include_load_tool_mix_warning()
    {
        var r1 = CreateAcceptedResultBase("kestrel", "Kestrel", "scenario1", "S1", "h3", 1, 1, 1, 100, 1, 2, 3, 1.5, 1000);
        r1 = r1 with { LoadTool = "h2load" };

        var r2 = CreateAcceptedResultBase("incursa", "Incursa", "scenario1", "S1", "h3", 1, 1, 1, 100, 1, 2, 3, 1.5, 1000);
        r2 = r2 with { LoadTool = "oha" };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, [], [], [r1, r2]);

        Assert.Contains(report.Warnings, w => w.Category == "comparability" && w.Message.Contains("Different load tools"));
    }

    [Fact]
    public void Artifact_index_contains_all_cells()
    {
        var results = new List<BenchmarkResult>
        {
            CreateAcceptedResult("kestrel", "Kestrel", "scenario1", "S1", "h3", 1, 1, 1, 100, 1, 2, 3, 1.5, 1000),
            CreateAcceptedResult("incursa", "Incursa", "scenario1", "S1", "h3", 1, 1, 1, 100, 1, 2, 3, 1.5, 1000),
        };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, [], [], results);

        Assert.Equal(2, report.ArtifactIndex.Count);

        var first = report.ArtifactIndex[0];
        Assert.Contains("kestrel", first.CellKey);
        Assert.Contains("scenario1", first.CellKey);
        Assert.NotEmpty(first.CellDirectory);
        Assert.NotEmpty(first.Files);
        Assert.Contains(first.Files, f => f.Name == "result.json");
        Assert.Contains(first.Files, f => f.Name == "validation.json");
    }

    [Fact]
    public void Markdown_output_contains_all_required_sections()
    {
        var cells = new List<RunCell>
        {
            CreateCell("kestrel-http3", "Kestrel", "scenario1", "S1", "h3", "http", 1, 1, 1),
            CreateCell("incursa-http3", "Incursa", "scenario1", "S1", "h3", "http", 1, 1, 1),
        };

        var compatibilities = new List<RunCellCompatibility>
        {
            RunCellCompatibility.Supported(),
            RunCellCompatibility.Supported(),
        };

        var results = new List<BenchmarkResult>
        {
            CreateAcceptedResult("kestrel-http3", "Kestrel", "scenario1", "S1", "h3", 1, 1, 1, 100, 1, 2, 3, 1.5, 1000),
            CreateAcceptedResult("incursa-http3", "Incursa", "scenario1", "S1", "h3", 1, 1, 1, 100, 1, 2, 3, 1.5, 1000),
        };

        var report = EvidenceReportBuilder.Build(
            "test-run",
            new DateTimeOffset(2026, 05, 30, 12, 0, 0, TimeSpan.Zero),
            null,
            "test-suite",
            "Test Suite",
            cells,
            compatibilities,
            results);

        var markdown = EvidenceReportMarkdownWriter.Write(report);

        Assert.Contains("# ProtocolLab Evidence Report: test-run", markdown);
        Assert.Contains("## 1. Run Identity", markdown);
        Assert.Contains("## 2. Matrix Summary", markdown);
        Assert.Contains("## 3. Validation Summary", markdown);
        Assert.Contains("## 4. Benchmark Acceptance Summary", markdown);
        Assert.Contains("## 5. Comparison Tables", markdown);
        Assert.Contains("## 6. Evidence Warnings", markdown);
        Assert.Contains("## 7. Artifact Index", markdown);
        Assert.Contains("test-suite", markdown);
        Assert.Contains("Test Suite", markdown);
        Assert.Contains("kestrel-http3", markdown);
        Assert.Contains("incursa-http3", markdown);
    }

    [Fact]
    public void Markdown_output_handles_empty_data()
    {
        var report = EvidenceReportBuilder.Build("empty", DateTimeOffset.UtcNow, null, null, null, [], [], []);

        var markdown = EvidenceReportMarkdownWriter.Write(report);

        Assert.Contains("# ProtocolLab Evidence Report: empty", markdown);
        Assert.Contains("## 1. Run Identity", markdown);
        Assert.Contains("## 5. Comparison Tables", markdown);
        Assert.Contains("No comparable groups found", markdown);
    }

    [Fact]
    public void Json_serialization_roundtrips()
    {
        var cells = new List<RunCell>
        {
            CreateCell("kestrel-http3", "Kestrel", "scenario1", "S1", "h3", "http", 16, 10, 1),
        };

        var compatibilities = new List<RunCellCompatibility>
        {
            RunCellCompatibility.Supported(),
        };

        var results = new List<BenchmarkResult>
        {
            CreateAcceptedResult("kestrel-http3", "Kestrel", "scenario1", "S1", "h3", 16, 10, 1, 5000, 2.5, 3.0, 4.0, 2.8, 100000),
        };

        var report = EvidenceReportBuilder.Build(
            "json-test",
            new DateTimeOffset(2026, 05, 30, 12, 0, 0, TimeSpan.Zero),
            null,
            "suite",
            "Suite",
            cells,
            compatibilities,
            results);

        var json = ResultJson.Serialize(report);
        var roundTrip = ResultJson.Deserialize<EvidenceReport>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(report.RunId, roundTrip.RunId);
        Assert.Equal(report.MatrixSummary.TotalCells, roundTrip.MatrixSummary.TotalCells);
        Assert.Equal(report.ValidationSummary.Passed, roundTrip.ValidationSummary.Passed);
        Assert.Equal(report.BenchmarkAcceptance.AcceptedBenchmarks, roundTrip.BenchmarkAcceptance.AcceptedBenchmarks);
        Assert.Equal(report.ComparisonGroups.Count, roundTrip.ComparisonGroups.Count);
        Assert.Equal(report.ArtifactIndex.Count, roundTrip.ArtifactIndex.Count);
    }

    [Fact]
    public void Benchmark_acceptance_rejects_invalid_comparability()
    {
        var result = CreateResult("impl", "scenario", "h3", 1, 1, 1,
            ValidationStatus.Passed, LoadToolExecutionStatuses.Succeeded, true);
        result = result with
        {
            Evidence = new BenchmarkEvidenceAssessment
            {
                EvidenceClass = BenchmarkEvidenceClasses.LocalSmoke,
                ComparabilityStatus = BenchmarkComparabilityStatuses.Invalid,
                ComparabilityWarnings = [BenchmarkEvidenceReasons.ValidationFailure]
            }
        };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, [], [], [result]);

        Assert.Equal(0, report.BenchmarkAcceptance.AcceptedBenchmarks);
        Assert.Equal(1, report.BenchmarkAcceptance.RejectedBenchmarks);
        Assert.Equal(0, report.BenchmarkAcceptance.NotRunValidationFailed);
    }

    [Fact]
    public void Comparison_groups_include_comparability_warnings()
    {
        var r1 = CreateAcceptedResult("kestrel", "Kestrel", "scenario1", "S1", "h3", 16, 10, 1, 5000, 2, 3, 4, 2.5, 100000);
        r1 = r1 with
        {
            Evidence = new BenchmarkEvidenceAssessment
            {
                EvidenceClass = BenchmarkEvidenceClasses.ExternalReferenceLocal,
                ComparabilityStatus = BenchmarkComparabilityStatuses.ComparableWithWarnings,
                ComparabilityWarnings = [BenchmarkEvidenceReasons.NoRepeatedStableMedian]
            }
        };

        var r2 = CreateAcceptedResult("incursa", "Incursa", "scenario1", "S1", "h3", 16, 10, 1, 4800, 2.1, 3.1, 4.1, 2.6, 95000);
        r2 = r2 with
        {
            Evidence = new BenchmarkEvidenceAssessment
            {
                EvidenceClass = BenchmarkEvidenceClasses.ExternalReferenceLocal,
                ComparabilityStatus = BenchmarkComparabilityStatuses.ComparableWithWarnings,
                ComparabilityWarnings = [BenchmarkEvidenceReasons.DockerNetworkLocal]
            }
        };

        var report = EvidenceReportBuilder.Build("run", DateTimeOffset.UtcNow, null, null, null, [], [], [r1, r2]);

        Assert.Single(report.ComparisonGroups);
        var group = report.ComparisonGroups[0];
        Assert.Equal(2, group.ComparabilityWarnings.Count);
        Assert.Contains(group.ComparabilityWarnings, w => w.Contains("no-repeated-stable-median"));
        Assert.Contains(group.ComparabilityWarnings, w => w.Contains("docker-network-local"));
    }

    private static RunCell CreateCell(
        string implementationId, string implementationName,
        string scenarioId, string scenarioName,
        string protocol, string family,
        int connections, int streamsPerConnection, int repetition)
    {
        return new RunCell(
            new ImplementationManifest
            {
                Id = implementationId,
                Name = implementationName,
                SupportedProtocols = [protocol],
                SupportedWorkloadFamilies = [family],
                Roles = ["server"]
            },
            new ScenarioDefinition
            {
                Id = scenarioId,
                Name = scenarioName,
                Family = family,
                Protocol = protocol,
                ImplementationRole = "server"
            },
            protocol,
            connections,
            streamsPerConnection,
            repetition,
            30,
            5,
            "clean");
    }

    private static BenchmarkResult CreateAcceptedResult(
        string implementationId, string implementationName,
        string scenarioId, string scenarioName,
        string protocol,
        int connections, int streamsPerConnection, int repetition,
        double requestsPerSecond, double p50, double p95, double p99, double mean, double throughput)
    {
        return CreateResult(implementationId, scenarioName, protocol, connections, streamsPerConnection, repetition,
            ValidationStatus.Passed, LoadToolExecutionStatuses.Succeeded, true,
            requestsPerSecond, p50, p95, p99, mean, throughput,
            implementationName, scenarioId);
    }

    private static BenchmarkResult CreateAcceptedResultBase(
        string implementationId, string implementationName,
        string scenarioId, string scenarioName,
        string protocol,
        int connections, int streamsPerConnection, int repetition,
        double requestsPerSecond, double p50, double p95, double p99, double mean, double throughput)
    {
        return CreateResult(implementationId, scenarioName, protocol, connections, streamsPerConnection, repetition,
            ValidationStatus.Passed, LoadToolExecutionStatuses.Succeeded, true,
            requestsPerSecond, p50, p95, p99, mean, throughput,
            implementationName, scenarioId);
    }

    private static BenchmarkResult CreateFailedResult(
        string implementationId, string implementationName,
        string scenarioId, string scenarioName,
        string protocol,
        int connections, int streamsPerConnection, int repetition)
    {
        return CreateResult(implementationId, scenarioName, protocol, connections, streamsPerConnection, repetition,
            ValidationStatus.Failed, LoadToolExecutionStatuses.Skipped, false,
            implementationName: implementationName, scenarioId: scenarioId);
    }

    private static BenchmarkResult CreateUnsupportedResult(
        string implementationId, string implementationName,
        string scenarioId, string scenarioName,
        string protocol,
        int connections, int streamsPerConnection, int repetition)
    {
        return CreateResult(implementationId, scenarioName, protocol, connections, streamsPerConnection, repetition,
            ValidationStatus.Unsupported, LoadToolExecutionStatuses.Skipped, false,
            implementationName: implementationName, scenarioId: scenarioId);
    }

    private static BenchmarkResult CreateLoadToolFailedResult(
        string implementationId, string implementationName,
        string scenarioId, string scenarioName,
        string protocol,
        int connections, int streamsPerConnection, int repetition)
    {
        return CreateResult(implementationId, scenarioName, protocol, connections, streamsPerConnection, repetition,
            ValidationStatus.Passed, LoadToolExecutionStatuses.Failed, false,
            implementationName: implementationName, scenarioId: scenarioId);
    }

    private static BenchmarkResult CreateParserFailedResult(
        string implementationId, string implementationName,
        string scenarioId, string scenarioName,
        string protocol,
        int connections, int streamsPerConnection, int repetition)
    {
        return CreateResult(implementationId, scenarioName, protocol, connections, streamsPerConnection, repetition,
            ValidationStatus.Passed, LoadToolExecutionStatuses.Succeeded, false,
            implementationName: implementationName, scenarioId: scenarioId);
    }

    private static BenchmarkResult CreateResult(
        string implementationId, string scenarioName, string protocol,
        int connections, int streamsPerConnection, int repetition,
        ValidationStatus validationStatus, string benchmarkStatus, bool parsedMetrics,
        double? requestsPerSecond = null, double? p50 = null, double? p95 = null,
        double? p99 = null, double? mean = null, double? throughput = null,
        string? implementationName = null, string? scenarioId = null)
    {
        var cell = new RunCell(
            new ImplementationManifest
            {
                Id = implementationId,
                Name = implementationName ?? implementationId,
                SupportedProtocols = [protocol],
                SupportedWorkloadFamilies = ["http.application"],
                Roles = ["server"]
            },
            new ScenarioDefinition
            {
                Id = scenarioId ?? "test-scenario",
                Name = scenarioName,
                Family = "http.application",
                Protocol = protocol,
                ImplementationRole = "server"
            },
            protocol,
            connections,
            streamsPerConnection,
            repetition,
            30,
            5,
            "clean");

        var artifacts = new Dictionary<string, string>
        {
            ["resultJson"] = $"C:/artifacts/runs/test-run/implementations/{implementationId}/{scenarioId ?? "test-scenario"}/{protocol}/c{connections}-s{streamsPerConnection}-r{repetition}/result.json",
            ["validationJson"] = $"C:/artifacts/runs/test-run/implementations/{implementationId}/{scenarioId ?? "test-scenario"}/{protocol}/c{connections}-s{streamsPerConnection}-r{repetition}/validation.json",
            ["protocolProofJson"] = $"C:/artifacts/runs/test-run/implementations/{implementationId}/{scenarioId ?? "test-scenario"}/{protocol}/c{connections}-s{streamsPerConnection}-r{repetition}/protocol-proof.json",
            ["loadToolStdout"] = $"C:/artifacts/runs/test-run/implementations/{implementationId}/{scenarioId ?? "test-scenario"}/{protocol}/c{connections}-s{streamsPerConnection}-r{repetition}/load.stdout.log",
            ["loadToolStderr"] = $"C:/artifacts/runs/test-run/implementations/{implementationId}/{scenarioId ?? "test-scenario"}/{protocol}/c{connections}-s{streamsPerConnection}-r{repetition}/load.stderr.log",
            ["targetStdout"] = $"C:/artifacts/runs/test-run/implementations/{implementationId}/{scenarioId ?? "test-scenario"}/{protocol}/c{connections}-s{streamsPerConnection}-r{repetition}/target.stdout.log",
            ["targetStderr"] = $"C:/artifacts/runs/test-run/implementations/{implementationId}/{scenarioId ?? "test-scenario"}/{protocol}/c{connections}-s{streamsPerConnection}-r{repetition}/target.stderr.log",
            ["manifestSnapshot"] = $"C:/artifacts/runs/test-run/implementations/{implementationId}/{scenarioId ?? "test-scenario"}/{protocol}/c{connections}-s{streamsPerConnection}-r{repetition}/manifest.json",
            ["scenarioSnapshot"] = $"C:/artifacts/runs/test-run/implementations/{implementationId}/{scenarioId ?? "test-scenario"}/{protocol}/c{connections}-s{streamsPerConnection}-r{repetition}/scenario.json",
            ["notes"] = $"C:/artifacts/runs/test-run/implementations/{implementationId}/{scenarioId ?? "test-scenario"}/{protocol}/c{connections}-s{streamsPerConnection}-r{repetition}/notes.txt",
        };

        var validationResult = new ScenarioValidationResult
        {
            ScenarioId = scenarioId ?? "test-scenario",
            TargetId = implementationId,
            AdapterId = "",
            Protocol = protocol,
            Status = validationStatus,
            Summary = validationStatus.ToString(),
            ProtocolProof = new ProtocolProofResult
            {
                Status = validationStatus == ValidationStatus.Passed ? ValidationStatus.Passed : ValidationStatus.NotApplicable,
                RequestedProtocol = protocol,
                ProvenProtocol = protocol,
                Method = "validation"
            }
        };

        var evidenceClass = parsedMetrics
            ? BenchmarkEvidenceClasses.ExternalReferenceLocal
            : BenchmarkEvidenceClasses.LocalSmoke;
        var comparabilityStatus = validationStatus == ValidationStatus.Passed && parsedMetrics
            ? BenchmarkComparabilityStatuses.ComparableWithWarnings
            : BenchmarkComparabilityStatuses.Invalid;

        var result = BenchmarkResult.FromCell(
            "test-run",
            cell,
            validationResult,
            "h2load",
            parsedMetrics,
            artifacts,
            loadToolMode: TargetKinds.Process,
            loadToolCategory: LoadToolCategories.ExternalReference,
            loadToolVersion: "h2load v1.0",
            benchmarkExecutionStatus: benchmarkStatus,
            benchmarkFailureReason: benchmarkStatus == LoadToolExecutionStatuses.Failed ? "Connection refused" : null)
        with
        {
            Metrics = new HttpMetrics
            {
                RequestsPerSecond = requestsPerSecond,
                LatencyP50Ms = p50,
                LatencyP95Ms = p95,
                LatencyP99Ms = p99,
                LatencyMeanMs = mean,
                ThroughputBytesPerSecond = throughput
            },
            Evidence = new BenchmarkEvidenceAssessment
            {
                EvidenceClass = evidenceClass,
                EvidenceReasons = [BenchmarkEvidenceReasons.ExternalReferenceLoadToolProven],
                ComparabilityStatus = comparabilityStatus,
                ComparabilityWarnings = [BenchmarkEvidenceReasons.NoRepeatedStableMedian]
            },
            ImplementationName = implementationName ?? implementationId,
            ScenarioName = scenarioName,
            ScenarioId = scenarioId ?? "test-scenario",
            Family = "http.application",
            LoadTool = "h2load"
        };

        return result;
    }
}
