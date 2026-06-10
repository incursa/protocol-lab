// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Cli;
using Incursa.ProtocolLab.Model;
using Incursa.ProtocolLab.Runner;
using Incursa.ProtocolLab.Tests.Fixtures.AdapterContractLab;
using Incursa.ProtocolLab.Tests.Fixtures.RunnerContractLab;

namespace Incursa.ProtocolLab.Tests;

[Collection(RunnerContractFixtureLabCollection.Name)]
public sealed class RunnerAdapterFixtureLabTests
{
    private const int AdapterControlPlanePort = 52381;

    private static string FixtureRoot => Path.Combine(TestPaths.RepoRoot, "tests", "Incursa.ProtocolLab.Tests", "Fixtures", "RunnerContractLab");

    [Fact]
    public async Task Adapter_list_and_check_surface_adapter_backed_targets()
    {
        var runner = new RunnerEngine();
        var list = runner.List(["implementations"], FixtureRoot);
        var check = await runner.CheckAsync(FixtureRoot);

        Assert.Equal(0, list.ExitCode);
        Assert.Contains(list.Messages, message => message.Text.Contains("fixture-adapter-http", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(list.Messages, message => message.Text.Contains("contract=adapter-v1", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(0, check.ExitCode);
        Assert.Contains(check.Messages, message => message.Text.Contains("Adapter-backed implementations:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(check.Messages, message => message.Text.Contains("fixture-adapter-http", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(check.Messages, message => message.Text.Contains("fixture-adapter-quic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Adapter_success_path_writes_control_plane_artifacts_and_report()
    {
        var output = CreateOutputDirectory("adapter-success");
        var runId = "adapter-success";
        await using var protocolHost = await FixtureHttpEndpointHost.StartAsync(0);
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions(protocolHost.BaseUrl, "fixture.adapter.success"));
        var options = BuildOptions(output, runId, "fixture-adapter-http", "fixture.adapter.success", "h1", "fixture-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-http", "fixture.adapter.success", "h1", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));
            var target = ResultJson.Deserialize<TargetExecutionResult>(await File.ReadAllTextAsync(paths.TargetExecutionJson));
            var report = ResultJson.Deserialize<RunReport>(await File.ReadAllTextAsync(Path.Combine(output, runId, "aggregate-results.json")));
            var summary = await File.ReadAllTextAsync(Path.Combine(output, runId, "summary.md"));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(ValidationStatus.Passed, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Succeeded, benchmark.BenchmarkExecutionStatus);
            Assert.True(benchmark.ParsedMetricsAvailable);
            Assert.Equal("adapter-v1", benchmark.TargetContract);
            Assert.Equal(adapterHost.Client.BaseAddress!.ToString(), benchmark.AdapterControlPlaneBaseUrl);
            Assert.Equal(protocolHost.BaseUrl, benchmark.TargetEffectiveBaseUrl);
            Assert.Contains(benchmark.AdapterEndpointTypes, value => string.Equals(value, "h1", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(benchmark.Evidence);
            Assert.Contains(benchmark.Evidence!.EvidenceReasons, value => string.Equals(value, BenchmarkEvidenceReasons.AdapterBackedTarget, StringComparison.OrdinalIgnoreCase));
            Assert.Equal("adapter-v1", target!.TargetContract);
            Assert.Equal(adapterHost.Client.BaseAddress!.ToString(), target.AdapterControlPlaneBaseUrl);
            Assert.Equal(protocolHost.BaseUrl, target.TargetEffectiveBaseUrl);
            Assert.NotNull(target.AdapterSessionId);
            Assert.Contains(target.AdapterEndpointTypes, value => string.Equals(value, "h1", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(paths.AdapterHealthJson));
            Assert.True(File.Exists(paths.AdapterManifestJson));
            Assert.True(File.Exists(paths.AdapterSessionCreateJson));
            Assert.True(File.Exists(paths.AdapterPrepareJson));
            Assert.True(File.Exists(paths.AdapterStartJson));
            Assert.True(File.Exists(paths.AdapterStatusJsonl));
            Assert.True(File.Exists(paths.AdapterEndpointsJson));
            Assert.True(File.Exists(paths.AdapterMetricsJson));
            Assert.True(File.Exists(paths.AdapterArtifactsJson));
            Assert.True(File.Exists(paths.AdapterStopJson));
            Assert.True(File.Exists(paths.AdapterDeleteJson));
            Assert.NotNull(report);
            Assert.Contains(report!.Aggregates, aggregate => string.Equals(aggregate.TargetContract, "adapter-v1", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("adapter control plane", summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("adapter-v1", summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Adapter_unsupported_path_returns_structured_unsupported_results()
    {
        var output = CreateOutputDirectory("adapter-unsupported");
        var runId = "adapter-unsupported";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("http://127.0.0.1:1", "fixture.adapter.unsupported"));
        var options = BuildOptions(output, runId, "fixture-adapter-http", "fixture.adapter.unsupported", "h1", "fixture-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-http", "fixture.adapter.unsupported", "h1", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));
            var target = ResultJson.Deserialize<TargetExecutionResult>(await File.ReadAllTextAsync(paths.TargetExecutionJson));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(ValidationStatus.Unsupported, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Skipped, benchmark.BenchmarkExecutionStatus);
            Assert.Contains("unsupported", benchmark.ValidationResult.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("adapter-v1", benchmark.TargetContract);
            Assert.True(target!.Unsupported);
            Assert.Equal("adapter-v1", target.TargetContract);
            Assert.True(File.Exists(paths.AdapterStopJson));
            Assert.True(File.Exists(paths.AdapterDeleteJson));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Adapter_prepare_failure_path_stops_without_loading_benchmark_data()
    {
        var output = CreateOutputDirectory("adapter-prepare-failure");
        var runId = "adapter-prepare-failure";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("http://127.0.0.1:1", "fixture.adapter.prepare-failure"));
        var options = BuildOptions(output, runId, "fixture-adapter-http", "fixture.adapter.prepare-failure", "h1", "fixture-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-http", "fixture.adapter.prepare-failure", "h1", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));
            var target = ResultJson.Deserialize<TargetExecutionResult>(await File.ReadAllTextAsync(paths.TargetExecutionJson));

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(ValidationStatus.Failed, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Skipped, benchmark.BenchmarkExecutionStatus);
            Assert.Contains("prepare", target!.Errors.FirstOrDefault() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(paths.AdapterStopJson));
            Assert.True(File.Exists(paths.AdapterDeleteJson));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Adapter_start_failure_path_stops_without_loading_benchmark_data()
    {
        var output = CreateOutputDirectory("adapter-start-failure");
        var runId = "adapter-start-failure";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("http://127.0.0.1:1", "fixture.adapter.start-failure"));
        var options = BuildOptions(output, runId, "fixture-adapter-http", "fixture.adapter.start-failure", "h1", "fixture-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-http", "fixture.adapter.start-failure", "h1", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));
            var target = ResultJson.Deserialize<TargetExecutionResult>(await File.ReadAllTextAsync(paths.TargetExecutionJson));

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(ValidationStatus.Failed, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Skipped, benchmark.BenchmarkExecutionStatus);
            Assert.True(target!.Failed);
            Assert.True(File.Exists(paths.AdapterStopJson));
            Assert.True(File.Exists(paths.AdapterDeleteJson));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Adapter_readiness_failure_path_skips_benchmark_and_cleans_up()
    {
        var output = CreateOutputDirectory("adapter-readiness-failure");
        var runId = "adapter-readiness-failure";
        await using var protocolHost = await FixtureHttpEndpointHost.StartAsync(0);
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions(protocolHost.BaseUrl, "fixture.adapter.readiness-failure"));
        var options = BuildOptions(output, runId, "fixture-adapter-http", "fixture.adapter.readiness-failure", "h1", "fixture-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-http", "fixture.adapter.readiness-failure", "h1", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));
            var target = ResultJson.Deserialize<TargetExecutionResult>(await File.ReadAllTextAsync(paths.TargetExecutionJson));

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(ValidationStatus.Failed, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Skipped, benchmark.BenchmarkExecutionStatus);
            Assert.True(target!.Failed || !target.Ready);
            Assert.True(File.Exists(paths.AdapterStopJson));
            Assert.True(File.Exists(paths.AdapterDeleteJson));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Adapter_metrics_collection_path_persists_metrics_snapshot()
    {
        var output = CreateOutputDirectory("adapter-metrics");
        var runId = "adapter-metrics";
        await using var protocolHost = await FixtureHttpEndpointHost.StartAsync(0);
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions(protocolHost.BaseUrl, "fixture.adapter.metrics"));
        var options = BuildOptions(output, runId, "fixture-adapter-http", "fixture.adapter.metrics", "h1", "fixture-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-http", "fixture.adapter.metrics", "h1", 1, 1, 1);
            var metrics = JsonSerializer.Deserialize<AdapterMetricsResponse>(await File.ReadAllTextAsync(paths.AdapterMetricsJson), ProtocolLabAdapterJson.Options);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(0, result.ExitCode);
            Assert.NotNull(metrics);
            Assert.Equal(AdapterResourceAvailability.Available, metrics!.Availability);
            Assert.Contains(metrics.Metrics, metric => metric.MetricId == "fixture.metric.requests");
            Assert.Equal(ValidationStatus.Passed, benchmark!.ValidationResult.Status);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Adapter_artifact_discovery_path_persists_artifact_index()
    {
        var output = CreateOutputDirectory("adapter-artifacts");
        var runId = "adapter-artifacts";
        await using var protocolHost = await FixtureHttpEndpointHost.StartAsync(0);
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions(protocolHost.BaseUrl, "fixture.adapter.artifacts"));
        var options = BuildOptions(output, runId, "fixture-adapter-http", "fixture.adapter.artifacts", "h1", "fixture-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-http", "fixture.adapter.artifacts", "h1", 1, 1, 1);
            var artifacts = JsonSerializer.Deserialize<AdapterArtifactsResponse>(await File.ReadAllTextAsync(paths.AdapterArtifactsJson), ProtocolLabAdapterJson.Options);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(0, result.ExitCode);
            Assert.NotNull(artifacts);
            Assert.Equal(AdapterResourceAvailability.Available, artifacts!.Availability);
            Assert.Contains(artifacts.Artifacts, artifact => artifact.ArtifactType == "log");
            Assert.Equal(ValidationStatus.Passed, benchmark!.ValidationResult.Status);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Adapter_cleanup_on_failure_still_stops_and_deletes_the_session()
    {
        var output = CreateOutputDirectory("adapter-cleanup");
        var runId = "adapter-cleanup";
        await using var protocolHost = await FixtureHttpEndpointHost.StartAsync(0);
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions(protocolHost.BaseUrl, "fixture.adapter.cleanup"));
        var options = BuildOptions(output, runId, "fixture-adapter-http", "fixture.adapter.cleanup", "h1", "fixture-load-fail", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-http", "fixture.adapter.cleanup", "h1", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));
            var target = ResultJson.Deserialize<TargetExecutionResult>(await File.ReadAllTextAsync(paths.TargetExecutionJson));

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(ValidationStatus.Passed, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Failed, benchmark.BenchmarkExecutionStatus);
            Assert.True(target!.Ready);
            Assert.True(File.Exists(paths.AdapterStopJson));
            Assert.True(File.Exists(paths.AdapterDeleteJson));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Adapter_quic_endpoint_discovery_flows_through_adapter_path()
    {
        var output = CreateOutputDirectory("adapter-quic");
        var runId = "adapter-quic";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("http://127.0.0.1:1", "fixture.adapter.quic-discovery"));
        var options = BuildOptions(output, runId, "fixture-adapter-quic", "fixture.adapter.quic-discovery", "quic", "fixture-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-quic", "fixture.adapter.quic-discovery", "quic", 1, 1, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));
            var target = ResultJson.Deserialize<TargetExecutionResult>(await File.ReadAllTextAsync(paths.TargetExecutionJson));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(ValidationStatus.Passed, benchmark!.ValidationResult.Status);
            Assert.Equal("adapter-v1", benchmark.TargetContract);
            Assert.Contains(benchmark.AdapterEndpointTypes, value => string.Equals(value, "quic", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("adapter-v1", target!.TargetContract);
            Assert.Contains(target.AdapterEndpointTypes, value => string.Equals(value, "quic", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(benchmark.Evidence);
            Assert.Contains(benchmark.Evidence!.EvidenceReasons, value => string.Equals(value, BenchmarkEvidenceReasons.AdapterBackedTarget, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    private static RunnerCommandOptions BuildOptions(
        string output,
        string runId,
        string implementationId,
        string scenarioId,
        string protocol,
        string loadTool,
        string? baseUrl = null,
        string? targetMode = null)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["root"] = FixtureRoot,
            ["implementations"] = implementationId,
            ["scenarios"] = scenarioId,
            ["protocol"] = protocol,
            ["load-tool"] = loadTool,
            ["output"] = output,
            ["run-id"] = runId
        };

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            values["base-url"] = baseUrl;
        }

        if (!string.IsNullOrWhiteSpace(targetMode))
        {
            values["target-mode"] = targetMode;
        }

        return new RunnerCommandOptions(values);
    }

    private static FakeAdapterHostOptions CreateAdapterHostOptions(string protocolEndpointBaseUrl, params string[] scenarioIds)
    {
        var httpEndpoint = CreateHttpEndpoint(protocolEndpointBaseUrl);
        var profiles = new Dictionary<string, FakeAdapterScenarioProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["fixture.adapter.success"] = FakeAdapterScenarioProfile.Success("fixture.adapter.success") with { Endpoints = [httpEndpoint] },
            ["fixture.adapter.unsupported"] = FakeAdapterScenarioProfile.Unsupported("fixture.adapter.unsupported") with { Endpoints = [httpEndpoint] },
            ["fixture.adapter.prepare-failure"] = FakeAdapterScenarioProfile.PrepareFailure("fixture.adapter.prepare-failure") with { Endpoints = [httpEndpoint] },
            ["fixture.adapter.start-failure"] = FakeAdapterScenarioProfile.StartFailure("fixture.adapter.start-failure") with { Endpoints = [httpEndpoint] },
            ["fixture.adapter.readiness-failure"] = FakeAdapterScenarioProfile.ReadinessFailure("fixture.adapter.readiness-failure") with { Endpoints = [httpEndpoint] },
            ["fixture.adapter.metrics"] = FakeAdapterScenarioProfile.MetricsProfile("fixture.adapter.metrics") with { Endpoints = [httpEndpoint] },
            ["fixture.adapter.artifacts"] = FakeAdapterScenarioProfile.ArtifactsProfile("fixture.adapter.artifacts") with { Endpoints = [httpEndpoint] },
            ["fixture.adapter.cleanup"] = FakeAdapterScenarioProfile.Cleanup("fixture.adapter.cleanup") with { Endpoints = [httpEndpoint] },
            ["fixture.adapter.quic-discovery"] = FakeAdapterScenarioProfile.Success("fixture.adapter.quic-discovery") with
            {
                Endpoints = [FakeAdapterData.CreateQuicEndpoint()]
            }
        };

        foreach (var scenarioId in scenarioIds)
        {
            if (!profiles.ContainsKey(scenarioId))
            {
                throw new InvalidOperationException($"No fixture adapter profile exists for scenario '{scenarioId}'.");
            }
        }

        return FakeAdapterHostOptions.CreateDefault() with
        {
            UseRealNetworkServer = true,
            ControlPlanePort = AdapterControlPlanePort,
            ScenarioProfiles = profiles
        };
    }

    private static AdapterEndpoint CreateHttpEndpoint(string baseUrl)
    {
        var uri = new Uri(baseUrl, UriKind.Absolute);
        return new AdapterEndpoint
        {
            EndpointId = "endpoint-http-001",
            Purpose = "test",
            Scheme = "http",
            Protocol = "h1",
            Host = uri.Host,
            Port = uri.Port,
            Tls = null
        };
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
            new ScenarioDefinition { Id = scenarioId, Name = scenarioId, Family = "fixture.adapter", Version = "1.0", Protocol = protocol, ImplementationRole = "server", NetworkProfile = "clean" },
            protocol,
            connections,
            streams,
            repetition,
            1,
            0,
            "clean")
        {
            ExecutionProfile = TestPaths.ExpectedExecutionProfile
        };

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
