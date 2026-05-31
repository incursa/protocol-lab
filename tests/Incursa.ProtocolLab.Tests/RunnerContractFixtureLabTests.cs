// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Cli;
using Incursa.ProtocolLab.Model;
using Incursa.ProtocolLab.Runner;

namespace Incursa.ProtocolLab.Tests;

[Collection(RunnerContractFixtureLabCollection.Name)]
public sealed class RunnerContractFixtureLabTests
{
    private static string FixtureRoot => Path.Combine(TestPaths.RepoRoot, "tests", "Incursa.ProtocolLab.Tests", "Fixtures", "RunnerContractLab");

    [Fact]
    public async Task Fixture_success_path_writes_artifacts_and_report()
    {
        var output = CreateOutputDirectory("fixture-success");
        var runId = "fixture-success";
        var options = BuildOptions(output, runId, "fixture-http-success", "fixture.http.success", "h1", "fixture-load-success");
        var events = new RecordingRunnerEventSink();

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options, events));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(RunnerCommandKind.Run, result.Kind);
            Assert.NotEmpty(result.Artifacts);
            Assert.NotEmpty(events.Events);
            Assert.Contains(events.Events, @event => @event.CommandKind == RunnerCommandKind.Run);

            var paths = GetCellPaths(output, runId, "fixture-http-success", "fixture.http.success", "h1", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.NotNull(benchmark);
            Assert.Equal(ValidationStatus.Passed, benchmark.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Succeeded, benchmark.BenchmarkExecutionStatus);
            Assert.True(benchmark.ParsedMetricsAvailable);
            Assert.Equal("fixture-load-success", benchmark.LoadTool);
            Assert.True(File.Exists(Path.Combine(output, runId, "run.json")));
            Assert.True(File.Exists(Path.Combine(output, runId, "aggregate-results.json")));
            Assert.True(File.Exists(Path.Combine(output, runId, "summary.md")));
            Assert.Contains("fixture-http-success", await File.ReadAllTextAsync(Path.Combine(output, runId, "summary.md")));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public void Fixture_compatibility_classification_path_reports_unsupported_and_incompatible_cells()
    {
        var implementations = ManifestCatalog.Load(Path.Combine(FixtureRoot, "implementations"));
        var scenarios = ScenarioCatalog.Load(Path.Combine(FixtureRoot, "scenarios"));
        var networkProfiles = NetworkProfileCatalog.Load(Path.Combine(FixtureRoot, "scenarios", "network", "profiles"));
        var loadTools = LoadToolCatalog.Load(Path.Combine(FixtureRoot, "load-tools"));

        var unsupportedCell = BuildCell(
            implementations.Single(manifest => manifest.Id == "fixture-http-unsupported"),
            scenarios.Single(scenario => scenario.Id == "fixture.http.success"),
            "h1");
        var incompatibleCell = BuildCell(
            implementations.Single(manifest => manifest.Id == "fixture-http-success"),
            scenarios.Single(scenario => scenario.Id == "fixture.http.incompatible-profile"),
            "h1");

        var unsupported = CompatibilityClassifier.Classify(unsupportedCell, loadTools);
        var incompatible = CompatibilityClassifier.Classify(incompatibleCell, loadTools, networkProfiles: networkProfiles);
        var unavailable = CompatibilityClassifier.Classify(BuildCell(
            implementations.Single(manifest => manifest.Id == "fixture-http-success"),
            scenarios.Single(scenario => scenario.Id == "fixture.http.success"),
            "h1"), loadTools, requestedLoadTool: "fixture-load-does-not-exist");

        Assert.Equal(RunCellCompatibilityStatuses.MissingCapability, unsupported.Status);
        Assert.Contains("protocol 'h1' is not supported", unsupported.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RunCellCompatibilityStatuses.Incompatible, incompatible.Status);
        Assert.Contains("was not found", incompatible.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RunCellCompatibilityStatuses.MissingLoadTool, unavailable.Status);
        Assert.Contains("fixture-load-does-not-exist", unavailable.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fixture_readiness_failure_path_preserves_diagnostics()
    {
        var output = CreateOutputDirectory("fixture-readiness-timeout");
        var runId = "fixture-readiness-timeout";
        var options = BuildOptions(output, runId, "fixture-http-readiness-timeout", "fixture.http.success", "h1", "fixture-load-success");

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-http-readiness-timeout", "fixture.http.success", "h1", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));
            var target = ResultJson.Deserialize<TargetExecutionResult>(await File.ReadAllTextAsync(paths.TargetExecutionJson));

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(ValidationStatus.Failed, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Skipped, benchmark.BenchmarkExecutionStatus);
            Assert.False(benchmark.ParsedMetricsAvailable);
            Assert.True(target!.Failed || !target.Ready);
            Assert.True(File.Exists(paths.TargetStdout));
            Assert.True(File.Exists(paths.TargetStderr));
            Assert.Contains("Benchmark was not accepted because validation did not pass.", benchmark.Warnings);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Fixture_validation_failure_blocks_accepted_benchmark_data()
    {
        var output = CreateOutputDirectory("fixture-validation-fail");
        var runId = "fixture-validation-fail";
        var options = BuildOptions(output, runId, "fixture-http-success", "fixture.http.validation-fail", "h1", "fixture-load-success");

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-http-success", "fixture.http.validation-fail", "h1", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(ValidationStatus.Failed, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Skipped, benchmark.BenchmarkExecutionStatus);
            Assert.False(benchmark.ParsedMetricsAvailable);
            Assert.Contains("validation did not pass", await File.ReadAllTextAsync(paths.LoadToolStderr), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Fixture_validation_unsupported_path_is_proven_by_h3_fixture()
    {
        var output = CreateOutputDirectory("fixture-validation-unsupported");
        var runId = "fixture-validation-unsupported";
        var options = BuildOptions(output, runId, "fixture-http-h3-target", "fixture.http.h3-contract", "h3", "fixture-load-success");

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-http-h3-target", "fixture.http.h3-contract", "h3", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(ValidationStatus.Unsupported, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Skipped, benchmark.BenchmarkExecutionStatus);
            Assert.False(benchmark.ParsedMetricsAvailable);
            Assert.Contains("no H3 protocol validator or load generator is implemented yet", benchmark.ValidationResult.Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Fixture_load_parse_failure_preserves_raw_output()
    {
        var output = CreateOutputDirectory("fixture-load-parsefail");
        var runId = "fixture-load-parsefail";
        var options = BuildOptions(output, runId, "fixture-http-success", "fixture.http.success", "h1", "fixture-load-parsefail");

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-http-success", "fixture.http.success", "h1", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(ValidationStatus.Passed, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Succeeded, benchmark.BenchmarkExecutionStatus);
            Assert.False(benchmark.ParsedMetricsAvailable);
            Assert.Contains("fixture parse failure", await File.ReadAllTextAsync(paths.LoadToolStdout), StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(paths.LoadToolStderr));
            Assert.True(File.Exists(paths.LoadToolExecutionJson));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Fixture_artifact_and_report_path_is_deterministic()
    {
        var output = CreateOutputDirectory("fixture-report");
        var runId = "fixture-report";
        var options = BuildOptions(output, runId, "fixture-http-success", "fixture.http.success", "h1", "fixture-load-success");

        try
        {
            var runResult = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var reportResult = await RunInRepositoryRootAsync(() => Task.FromResult(new RunnerEngine().Report(FixtureRoot, new RunnerCommandOptions(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["output"] = output,
                ["run-id"] = runId
            }))));

            Assert.Equal(0, runResult.ExitCode);
            Assert.Equal(0, reportResult.ExitCode);
            Assert.Contains(reportResult.Artifacts, artifact => artifact.Kind == "summary" && File.Exists(artifact.Path));
            Assert.True(File.Exists(Path.Combine(output, runId, "aggregate-results.json")));
            Assert.True(File.Exists(Path.Combine(output, runId, "summary.md")));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Fixture_event_sink_captures_progress_without_console_output()
    {
        var output = CreateOutputDirectory("fixture-events");
        var runId = "fixture-events";
        var events = new RecordingRunnerEventSink();
        var options = BuildOptions(output, runId, "fixture-http-success", "fixture.http.success", "h1", "fixture-load-success");

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options, events));

            Assert.Equal(0, result.ExitCode);
            Assert.NotEmpty(events.Events);
            Assert.Contains(events.Events, @event => @event.CommandKind == RunnerCommandKind.Run);
            Assert.Contains(events.Events, @event => @event.Message.Contains("fixture-http-success", StringComparison.OrdinalIgnoreCase) || @event.Message.Contains("fixture.http.success", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Fixture_cli_smoke_path_renders_fixture_output()
    {
        var output = CreateOutputDirectory("fixture-cli");
        var runId = "fixture-cli";
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var exitCode = await RunInRepositoryRootAsync(() => ProtocolLabCommand.RunAsync([
                "run",
                "--root", FixtureRoot,
                "--implementations", "fixture-http-success",
                "--scenarios", "fixture.http.success",
                "--protocol", "h1",
                "--load-tool", "fixture-load-success",
                "--output", output,
                "--run-id", runId]));

            Assert.Equal(0, exitCode);
            var outputText = writer.ToString();
            Assert.Contains("fixture-http-success/fixture.http.success/h1", outputText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("validation=passed", outputText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("benchmark=succeeded", outputText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
            DeleteDirectory(output);
        }
    }

    [Fact]
    public void Fixture_suite_document_is_loadable_without_runner_coupling()
    {
        var suite = YamlFile.Load<SuiteDefinition>(Path.Combine(FixtureRoot, "suites", "runner-contract.fixture.yaml"));

        Assert.Equal("runner-contract-fixture", suite.Id);
        Assert.Contains("fixture-http-success", suite.Implementations);
        Assert.Contains("fixture.http.success", suite.Scenarios);
        Assert.Contains(suite.LoadTools, tool => tool.Id == "fixture-load-success");
    }

    [Fact]
    public void Fixture_validation_unavailable_is_proven_by_the_fixture_coordinator()
    {
        var result = FixtureValidationCoordinator.Unavailable();

        Assert.Equal(ValidationStatus.InfrastructureFailure, result.Status);
        Assert.Contains("unavailable", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static RunnerCommandOptions BuildOptions(
        string output,
        string runId,
        string implementationId,
        string scenarioId,
        string protocol,
        string loadTool)
    {
        return new RunnerCommandOptions(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["root"] = FixtureRoot,
            ["implementations"] = implementationId,
            ["scenarios"] = scenarioId,
            ["protocol"] = protocol,
            ["load-tool"] = loadTool,
            ["output"] = output,
            ["run-id"] = runId
        });
    }

    private static RunCell BuildCell(ImplementationManifest implementation, ScenarioDefinition scenario, string protocol)
    {
        return new RunCell(implementation, scenario, protocol, 1, 1, 1, 1, 0, scenario.NetworkProfile);
    }

    private static ArtifactPaths GetCellPaths(
        string output,
        string runId,
        string implementationId,
        string scenarioId,
        string protocol,
        int connections,
        int streams,
        int repetition)
    {
        var cell = new RunCell(
            new ImplementationManifest { Id = implementationId, Name = implementationId },
            new ScenarioDefinition { Id = scenarioId, Name = scenarioId, Family = "fixture.http", Protocol = protocol, ImplementationRole = "server", NetworkProfile = "clean" },
            protocol,
            connections,
            streams,
            repetition,
            1,
            0,
            "clean");

        return ArtifactLayout.GetCellPaths(output, runId, cell);
    }

    private static string CreateOutputDirectory(string prefix)
    {
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-fixture-{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);
        return output;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task<T> RunInRepositoryRootAsync<T>(Func<Task<T>> action)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(TestPaths.RepoRoot);

        try
        {
            return await action();
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }
    }
}

internal static class FixtureValidationCoordinator
{
    public static ScenarioValidationResult Unavailable()
    {
        return new ScenarioValidationResult
        {
            ScenarioId = "",
            TargetId = "",
            AdapterId = "",
            Protocol = "",
            Status = ValidationStatus.InfrastructureFailure,
            Summary = "Fixture validator unavailable."
        };
    }
}
