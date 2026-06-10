// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Model;
using Incursa.ProtocolLab.Runner;
using Incursa.ProtocolLab.Tests.Fixtures.AdapterContractLab;

namespace Incursa.ProtocolLab.Tests;

[Collection(RunnerContractFixtureLabCollection.Name)]
public sealed class RawQuicTransportExecutionTests
{
    private static string FixtureRoot => Path.Combine(TestPaths.RepoRoot, "tests", "Incursa.ProtocolLab.Tests", "Fixtures", "RunnerContractLab");

    [Fact]
    public void Plan_expansion_selects_enabled_raw_quic_transport_slice()
    {
        var implementations = ManifestCatalog.Load(Path.Combine(FixtureRoot, "implementations"));
        var scenarios = ScenarioCatalog.Load(Path.Combine(TestPaths.RepoRoot, "scenarios"));
        var cells = new RunPlanBuilder().Build(
            implementations,
            scenarios,
            new MatrixOptions(
                ImplementationIds: ["fixture-adapter-raw-quic-transport"],
                ScenarioIds: ["quic.transport.multiplex.100x64kb", "quic.transport.duplex-streams"],
                Protocols: ["quic"]));

        Assert.Equal(2, cells.Count);
        Assert.Contains(cells, cell => cell.Scenario.Id == "quic.transport.multiplex.100x64kb" && cell.StreamsPerConnection == 100);
        Assert.Contains(cells, cell => cell.Scenario.Id == "quic.transport.duplex-streams" && cell.StreamsPerConnection == 16);
    }

    [Fact]
    public async Task Unsupported_raw_quic_transport_scenarios_stay_explicit()
    {
        var implementation = YamlFile.Load<ImplementationManifest>(
            Path.Combine(FixtureRoot, "implementations", "fixture-adapter-raw-quic-transport.yaml"));
        var scenario = YamlFile.Load<ScenarioDefinition>(
            Path.Combine(TestPaths.RepoRoot, "scenarios", "quic", "transport", "stream-throughput.yaml"));
        var cell = new RunCell(implementation, scenario, "quic", 1, 1, 1, 30, 5, "clean");

        var validation = await QuicTransportValidator.ValidateAsync(cell, "quic://127.0.0.1:4433");

        Assert.Equal(ValidationStatus.Unsupported, validation.Status);
        Assert.Contains("not enabled yet", validation.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quic.transport.multiplex.100x64kb", validation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Successful_raw_quic_fixture_load_is_accepted_with_artifacts_preserved()
    {
        var output = CreateOutputDirectory("raw-quic-success");
        var runId = "raw-quic-success";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions());
        var options = BuildOptions(
            output,
            runId,
            "quic.transport.duplex-streams",
            "fixture-raw-quic-load-success",
            adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "quic.transport.duplex-streams", streams: 16);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.NotNull(benchmark);
            Assert.True(
                result.ExitCode == 0,
                string.Join(Environment.NewLine, benchmark!.ValidationResult.Errors.Concat(benchmark.Warnings).Concat(result.Messages.Select(message => message.Text))));
            Assert.Equal(ValidationStatus.Passed, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Succeeded, benchmark.BenchmarkExecutionStatus);
            Assert.True(benchmark.ParsedMetricsAvailable);
            Assert.Equal(16, benchmark.Metrics.CompletedStreams);
            Assert.Equal(1_048_576, benchmark.Metrics.BytesSent);
            Assert.Equal(1_048_576, benchmark.Metrics.BytesReceived);
            Assert.Contains("fixture-raw-quic-load", await File.ReadAllTextAsync(paths.LoadToolStdout), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fixture raw quic stderr preserved", await File.ReadAllTextAsync(paths.LoadToolStderr), StringComparison.OrdinalIgnoreCase);
            var commandText = await File.ReadAllTextAsync(paths.LoadToolCommandTxt);
            Assert.False(string.IsNullOrWhiteSpace(commandText));
            Assert.Contains("fixture-load-tool.ps1", commandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("--streams-per-connection 16", commandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("quic://127.0.0.1:4433", commandText, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(paths.H2loadCommandTxt));
            Assert.True(string.IsNullOrWhiteSpace(await File.ReadAllTextAsync(paths.H2loadCommandTxt)));
            Assert.Equal(paths.LoadToolCommandTxt, benchmark.Artifacts["loadToolCommand"]);
            Assert.Equal(paths.H2loadCommandTxt, benchmark.Artifacts["h2loadCommand"]);
            Assert.Equal(commandText, benchmark.LoadToolCommandLine);
            Assert.Contains("fixture-raw-quic-load-success", benchmark.LoadTool!, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(paths.CellDirectory, "load-tool.attempt-1.stdout.txt")));
            Assert.True(File.Exists(Path.Combine(paths.CellDirectory, "load-tool.attempt-1.stderr.txt")));
            Assert.Contains(benchmark.Warnings, warning => warning.Contains("qlog", StringComparison.OrdinalIgnoreCase) && warning.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Raw_quic_validation_gate_failure_rejects_benchmark_metrics_but_preserves_output()
    {
        var output = CreateOutputDirectory("raw-quic-validationfail");
        var runId = "raw-quic-validationfail";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions());
        var options = BuildOptions(
            output,
            runId,
            "quic.transport.duplex-streams",
            "fixture-raw-quic-load-validationfail",
            adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "quic.transport.duplex-streams", streams: 16);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(1, result.ExitCode);
            Assert.NotNull(benchmark);
            Assert.Equal(ValidationStatus.Failed, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Failed, benchmark.BenchmarkExecutionStatus);
            Assert.False(benchmark.ParsedMetricsAvailable);
            Assert.Null(benchmark.Metrics.BytesSent);
            Assert.Contains("Raw QUIC load-tool validation gates failed", benchmark.BenchmarkFailureReason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fixture-raw-quic-load", await File.ReadAllTextAsync(paths.LoadToolStdout), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fixture raw quic stderr preserved", await File.ReadAllTextAsync(paths.LoadToolStderr), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Package_backed_raw_quic_load_tool_records_command_and_provenance_artifacts()
    {
        var output = CreateOutputDirectory("raw-quic-package-artifacts");
        var runId = "raw-quic-package-artifacts";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions());
        var options = BuildOptions(
            output,
            runId,
            "quic.transport.duplex-streams",
            "fixture-raw-quic-package-load-success",
            adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "quic.transport.duplex-streams", streams: 16);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.NotNull(benchmark);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("fixture-raw-quic-package-load-success", benchmark!.LoadTool);
            Assert.Equal(LoadToolExecutionStatuses.Succeeded, benchmark.BenchmarkExecutionStatus);
            Assert.Equal(Path.GetFullPath(FixtureRoot), benchmark.LoadToolWorkingDirectory);
            Assert.Equal("raw-quic-json", benchmark.LoadToolParserId);
            Assert.Equal(paths.LoadToolCommandTxt, benchmark.Artifacts["loadToolCommand"]);
            Assert.Equal(paths.LoadToolExecutionJson, benchmark.Artifacts["loadToolExecution"]);
            Assert.Equal(paths.LoadToolStdout, benchmark.Artifacts["loadToolStdout"]);
            Assert.Equal(paths.LoadToolStderr, benchmark.Artifacts["loadToolStderr"]);

            var commandText = await File.ReadAllTextAsync(paths.LoadToolCommandTxt);
            Assert.False(string.IsNullOrWhiteSpace(commandText));
            Assert.Contains("scripts/fixture-load-tool.ps1", commandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("quic://127.0.0.1:4433", commandText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(commandText, benchmark.LoadToolCommandLine);

            var executionJson = await File.ReadAllTextAsync(paths.LoadToolExecutionJson);
            Assert.Contains("fixture-raw-quic-package-load-success", executionJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("RunnerContractLab", executionJson, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Raw_quic_parser_failure_is_classified_as_failed_validation()
    {
        var output = CreateOutputDirectory("raw-quic-parsefail");
        var runId = "raw-quic-parsefail";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions());
        var options = BuildOptions(
            output,
            runId,
            "quic.transport.duplex-streams",
            "fixture-raw-quic-load-parsefail",
            adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "quic.transport.duplex-streams", streams: 16);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(1, result.ExitCode);
            Assert.NotNull(benchmark);
            Assert.Equal(ValidationStatus.Failed, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Failed, benchmark.BenchmarkExecutionStatus);
            Assert.False(benchmark.ParsedMetricsAvailable);
            Assert.Contains("parse failure", await File.ReadAllTextAsync(paths.LoadToolStdout), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("parse stderr preserved", await File.ReadAllTextAsync(paths.LoadToolStderr), StringComparison.OrdinalIgnoreCase);
            Assert.Contains(benchmark.ValidationResult.Errors, error => error.Contains("parseable metrics", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    private static RunnerCommandOptions BuildOptions(
        string output,
        string runId,
        string scenarioId,
        string loadTool,
        string baseUrl)
    {
        return new RunnerCommandOptions(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["root"] = FixtureRoot,
            ["implementations"] = "fixture-adapter-raw-quic-transport",
            ["scenarios"] = scenarioId,
            ["protocol"] = "quic",
            ["load-tool"] = loadTool,
            ["output"] = output,
            ["run-id"] = runId,
            ["base-url"] = baseUrl,
            ["duration"] = "1",
            ["warmup"] = "0",
            ["repetitions"] = "1"
        });
    }

    private static FakeAdapterHostOptions CreateAdapterHostOptions()
    {
        var quicEndpoint = FakeAdapterData.CreateQuicEndpoint() with
        {
            Host = "127.0.0.1",
            Port = 4433,
            Tls = new AdapterTlsNotes
            {
                CertificateMode = "fixture-self-signed",
                CertificateNotes = "Fake QUIC endpoint for raw QUIC runner tests.",
                Sni = "fixture-quic"
            }
        };

        var profiles = new Dictionary<string, FakeAdapterScenarioProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["quic.transport.duplex-streams"] = FakeAdapterScenarioProfile.Success("quic.transport.duplex-streams") with { Endpoints = [quicEndpoint] },
            ["quic.transport.multiplex.100x64kb"] = FakeAdapterScenarioProfile.Success("quic.transport.multiplex.100x64kb") with { Endpoints = [quicEndpoint] }
        };

        return FakeAdapterHostOptions.CreateDefault() with
        {
            UseRealNetworkServer = true,
            ScenarioProfiles = profiles
        };
    }

    private static ArtifactPaths GetCellPaths(string output, string runId, string scenarioId, int streams)
    {
        var implementation = YamlFile.Load<ImplementationManifest>(
            Path.Combine(FixtureRoot, "implementations", "fixture-adapter-raw-quic-transport.yaml"));
        var scenarioPath = scenarioId == "quic.transport.multiplex.100x64kb"
            ? Path.Combine(TestPaths.RepoRoot, "scenarios", "quic", "transport", "multiplex-100-streams.yaml")
            : Path.Combine(TestPaths.RepoRoot, "scenarios", "quic", "transport", "duplex-streams.yaml");
        var scenario = YamlFile.Load<ScenarioDefinition>(scenarioPath);
        var cell = new RunCell(implementation, scenario, "quic", 1, streams, 1, 1, 0, "clean")
        {
            ExecutionProfile = TestPaths.ExpectedExecutionProfile
        };

        return ArtifactLayout.GetCellPaths(output, runId, cell);
    }

    private static string CreateOutputDirectory(string prefix)
    {
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-{prefix}-{Guid.NewGuid():N}");
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
        var original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(TestPaths.RepoRoot);
        try
        {
            return await action();
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }
}
