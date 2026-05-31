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
public sealed class RawQuicFoundationTests
{
    private const int AdapterControlPlanePort = 52382;

    private static string FixtureRoot => Path.Combine(TestPaths.RepoRoot, "tests", "Incursa.ProtocolLab.Tests", "Fixtures", "RunnerContractLab");

    [Fact]
    public async Task Quic_handshake_passes_through_adapter_backed_runner_flow()
    {
        await RunQuicScenarioAsync("fixture.quic.handshake", "fixture-adapter-quic-handshake", validationStatus: ValidationStatus.Passed, streams: 0);
    }

    [Fact]
    public async Task Quic_bidirectional_echo_passes_through_adapter_backed_runner_flow()
    {
        await RunQuicScenarioAsync("fixture.quic.bidirectional-echo", "fixture-adapter-quic-echo", validationStatus: ValidationStatus.Passed, streams: 1);
    }

    [Fact]
    public async Task Quic_bidirectional_bulk_passes_through_adapter_backed_runner_flow()
    {
        await RunQuicScenarioAsync("fixture.quic.bidirectional-bulk", "fixture-adapter-quic-bulk", validationStatus: ValidationStatus.Passed, streams: 16);
    }

    [Fact]
    public async Task Quic_unidirectional_send_passes_through_adapter_backed_runner_flow()
    {
        await RunQuicScenarioAsync("fixture.quic.unidirectional-send", "fixture-adapter-quic-unidirectional", validationStatus: ValidationStatus.Passed, streams: 4);
    }

    [Fact]
    public async Task Quic_unsupported_scenario_is_reported_structurally()
    {
        var output = CreateOutputDirectory("quic-unsupported");
        var runId = "quic-unsupported";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("quic://127.0.0.1:4433", "fixture.quic.unsupported"));
        var options = BuildOptions(output, runId, "fixture-adapter-quic-unsupported", "fixture.quic.unsupported", "quic", "fixture-quic-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-quic-unsupported", "fixture.quic.unsupported", "quic", 1, 0, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(0, result.ExitCode);
            Assert.NotNull(benchmark);
            Assert.Equal(ValidationStatus.Unsupported, benchmark!.ValidationResult.Status);
            Assert.Equal(LoadToolExecutionStatuses.Skipped, benchmark.BenchmarkExecutionStatus);
            Assert.Contains("unsupported", benchmark.ValidationResult.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(paths.AdapterStopJson));
            Assert.True(File.Exists(paths.AdapterDeleteJson));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Quic_endpoint_metadata_includes_quic_fields()
    {
        var output = CreateOutputDirectory("quic-metadata");
        var runId = "quic-metadata";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("quic://127.0.0.1:4433", "fixture.quic.handshake"));
        var options = BuildOptions(output, runId, "fixture-adapter-quic-handshake", "fixture.quic.handshake", "quic", "fixture-quic-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-quic-handshake", "fixture.quic.handshake", "quic", 1, 0, 1);
            var endpoints = JsonSerializer.Deserialize<AdapterEndpointsResponse>(await File.ReadAllTextAsync(paths.AdapterEndpointsJson), ProtocolLabAdapterJson.Options);

            Assert.NotNull(endpoints);
            var endpoint = Assert.Single(endpoints!.Endpoints);
            Assert.Equal("quic", endpoint.Scheme);
            Assert.Equal("quic", endpoint.Protocol);
            Assert.Equal("127.0.0.1", endpoint.Host);
            Assert.Equal(4433, endpoint.Port);
            Assert.NotNull(endpoint.Tls);
            Assert.Equal("fixture-quic", endpoint.Tls!.Sni);

            Assert.True(endpoint.Extensions.ContainsKey("alpn"));
            Assert.True(endpoint.Extensions.ContainsKey("transport"));
            Assert.Equal("udp", endpoint.Extensions["transport"].GetString());

            Assert.True(endpoint.Extensions.ContainsKey("streamBehavior"));
            Assert.True(endpoint.Extensions.ContainsKey("supportedStreamDirections"));
            Assert.True(endpoint.Extensions.ContainsKey("datagramSupported"));
            Assert.True(endpoint.Extensions.ContainsKey("zeroRttSupported"));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Quic_protocol_endpoint_differs_from_control_plane_url()
    {
        var output = CreateOutputDirectory("quic-url-separation");
        var runId = "quic-url-separation";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("quic://127.0.0.1:4433", "fixture.quic.handshake"));
        var options = BuildOptions(output, runId, "fixture-adapter-quic-handshake", "fixture.quic.handshake", "quic", "fixture-quic-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-quic-handshake", "fixture.quic.handshake", "quic", 1, 0, 1);
            var endpoints = JsonSerializer.Deserialize<AdapterEndpointsResponse>(await File.ReadAllTextAsync(paths.AdapterEndpointsJson), ProtocolLabAdapterJson.Options);

            Assert.Equal(0, result.ExitCode);
            Assert.NotNull(endpoints);
            var protocolEndpoint = Assert.Single(endpoints!.Endpoints);
            Assert.NotEqual(adapterHost.Client.BaseAddress!.Port, protocolEndpoint.Port);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Quic_artifacts_are_persisted_for_supported_scenarios()
    {
        var output = CreateOutputDirectory("quic-artifacts");
        var runId = "quic-artifacts";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("quic://127.0.0.1:4433", "fixture.quic.bidirectional-echo"));
        var options = BuildOptions(output, runId, "fixture-adapter-quic-echo", "fixture.quic.bidirectional-echo", "quic", "fixture-quic-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-quic-echo", "fixture.quic.bidirectional-echo", "quic", 1, 1, 1);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(paths.AdapterHealthJson));
            Assert.True(File.Exists(paths.AdapterManifestJson));
            Assert.True(File.Exists(paths.AdapterSessionCreateJson));
            Assert.True(File.Exists(paths.AdapterPrepareJson));
            Assert.True(File.Exists(paths.AdapterStartJson));
            Assert.True(File.Exists(paths.AdapterEndpointsJson));
            Assert.True(File.Exists(paths.AdapterMetricsJson));
            Assert.True(File.Exists(paths.AdapterArtifactsJson));
            Assert.True(File.Exists(paths.AdapterStopJson));
            Assert.True(File.Exists(paths.AdapterDeleteJson));
            Assert.True(File.Exists(paths.ResultJson));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Quic_metrics_are_available_for_supported_scenarios()
    {
        var output = CreateOutputDirectory("quic-metrics");
        var runId = "quic-metrics";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("quic://127.0.0.1:4433", "fixture.quic.bidirectional-bulk"));
        var options = BuildOptions(output, runId, "fixture-adapter-quic-bulk", "fixture.quic.bidirectional-bulk", "quic", "fixture-quic-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-quic-bulk", "fixture.quic.bidirectional-bulk", "quic", 1, 16, 1);
            var metrics = JsonSerializer.Deserialize<AdapterMetricsResponse>(await File.ReadAllTextAsync(paths.AdapterMetricsJson), ProtocolLabAdapterJson.Options);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(0, result.ExitCode);
            Assert.NotNull(metrics);
            Assert.Equal(AdapterResourceAvailability.Available, metrics!.Availability);
            Assert.Equal(ValidationStatus.Passed, benchmark!.ValidationResult.Status);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Quic_cleanup_works_on_success()
    {
        var output = CreateOutputDirectory("quic-cleanup-success");
        var runId = "quic-cleanup-success";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("quic://127.0.0.1:4433", "fixture.quic.handshake"));
        var options = BuildOptions(output, runId, "fixture-adapter-quic-handshake", "fixture.quic.handshake", "quic", "fixture-quic-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-quic-handshake", "fixture.quic.handshake", "quic", 1, 0, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(0, result.ExitCode);
            Assert.NotNull(benchmark);
            Assert.Equal(ValidationStatus.Passed, benchmark!.ValidationResult.Status);
            Assert.True(File.Exists(paths.AdapterStopJson));
            Assert.True(File.Exists(paths.AdapterDeleteJson));
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task Quic_scenarios_do_not_require_http_endpoints()
    {
        var output = CreateOutputDirectory("quic-no-http");
        var runId = "quic-no-http";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("quic://127.0.0.1:4433", "fixture.quic.handshake"));
        var options = BuildOptions(output, runId, "fixture-adapter-quic-handshake", "fixture.quic.handshake", "quic", "fixture-quic-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, "fixture-adapter-quic-handshake", "fixture.quic.handshake", "quic", 1, 0, 1);
            var endpoints = JsonSerializer.Deserialize<AdapterEndpointsResponse>(await File.ReadAllTextAsync(paths.AdapterEndpointsJson), ProtocolLabAdapterJson.Options);
            var target = ResultJson.Deserialize<TargetExecutionResult>(await File.ReadAllTextAsync(paths.TargetExecutionJson));
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(0, result.ExitCode);
            Assert.NotNull(endpoints);
            Assert.DoesNotContain(endpoints!.Endpoints, ep =>
                ep.Scheme == "http" || ep.Scheme == "https");
            Assert.NotNull(benchmark);
            Assert.Equal(ValidationStatus.Passed, benchmark!.ValidationResult.Status);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    private async Task RunQuicScenarioAsync(string scenarioId, string implementationId, ValidationStatus validationStatus, int streams = 1)
    {
        var output = CreateOutputDirectory($"quic-{scenarioId.Replace(".", "-")}");
        var runId = $"quic-{scenarioId.Replace(".", "-")}";
        await using var adapterHost = await FakeAdapterHost.StartAsync(CreateAdapterHostOptions("quic://127.0.0.1:4433", scenarioId));
        var options = BuildOptions(output, runId, implementationId, scenarioId, "quic", "fixture-quic-load-success", adapterHost.Client.BaseAddress!.ToString());

        try
        {
            var result = await RunInRepositoryRootAsync(() => new RunnerEngine().RunBenchmarkAsync(FixtureRoot, options));
            var paths = GetCellPaths(output, runId, implementationId, scenarioId, "quic", 1, streams, 1);
            var benchmark = ResultJson.Deserialize<BenchmarkResult>(await File.ReadAllTextAsync(paths.ResultJson));

            Assert.Equal(0, result.ExitCode);
            Assert.NotNull(benchmark);
            Assert.Equal(validationStatus, benchmark!.ValidationResult.Status);
            Assert.Equal("adapter-v1", benchmark.TargetContract);
            Assert.NotNull(benchmark.Evidence);
            Assert.Contains(benchmark.Evidence!.EvidenceReasons, value => string.Equals(value, BenchmarkEvidenceReasons.AdapterBackedTarget, StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(paths.AdapterStopJson));
            Assert.True(File.Exists(paths.AdapterDeleteJson));
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
        var quicEndpoint = new AdapterEndpoint
        {
            EndpointId = "endpoint-quic-001",
            Purpose = "test",
            Scheme = "quic",
            Protocol = "quic",
            Host = "127.0.0.1",
            Port = 4433,
            NetworkMode = "process-local",
            BindMode = "loopback",
            Tls = new AdapterTlsNotes
            {
                CertificateMode = "fixture-self-signed",
                CertificateNotes = "Fake QUIC endpoint for raw QUIC foundation tests.",
                Sni = "fixture-quic"
            },
            Extensions = new Dictionary<string, JsonElement>
            {
                ["alpn"] = ProtocolLabAdapterJson.SerializeValue((IReadOnlyList<string>)new[] { "quic" }),
                ["transport"] = ProtocolLabAdapterJson.SerializeValue("udp"),
                ["streamBehavior"] = ProtocolLabAdapterJson.SerializeValue("bidirectional"),
                ["supportedStreamDirections"] = ProtocolLabAdapterJson.SerializeValue((IReadOnlyList<string>)new[] { "bidirectional", "unidirectional" }),
                ["datagramSupported"] = ProtocolLabAdapterJson.SerializeValue(false),
                ["zeroRttSupported"] = ProtocolLabAdapterJson.SerializeValue(false)
            }
        };

        var profiles = new Dictionary<string, FakeAdapterScenarioProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["fixture.quic.handshake"] = FakeAdapterScenarioProfile.Success("fixture.quic.handshake") with { Endpoints = [quicEndpoint] },
            ["fixture.quic.bidirectional-echo"] = FakeAdapterScenarioProfile.Success("fixture.quic.bidirectional-echo") with { Endpoints = [quicEndpoint] },
            ["fixture.quic.bidirectional-bulk"] = FakeAdapterScenarioProfile.Success("fixture.quic.bidirectional-bulk") with { Endpoints = [quicEndpoint] },
            ["fixture.quic.unidirectional-send"] = FakeAdapterScenarioProfile.Success("fixture.quic.unidirectional-send") with { Endpoints = [quicEndpoint] },
            ["fixture.quic.unsupported"] = FakeAdapterScenarioProfile.Unsupported("fixture.quic.unsupported")
        };

        return FakeAdapterHostOptions.CreateDefault() with
        {
            UseRealNetworkServer = true,
            ControlPlanePort = AdapterControlPlanePort,
            ScenarioProfiles = profiles
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
            new ScenarioDefinition { Id = scenarioId, Name = scenarioId, Family = "fixture.quic", Version = "1.0", Protocol = protocol, ImplementationRole = "server", NetworkProfile = "clean" },
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
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-raw-quic-{prefix}-{Guid.NewGuid():N}");
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
