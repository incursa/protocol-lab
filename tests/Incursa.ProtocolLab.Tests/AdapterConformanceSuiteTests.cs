// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net.Http;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Conformance;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Tests.Fixtures.AdapterContractLab;

namespace Incursa.ProtocolLab.Tests;

public sealed class AdapterConformanceSuiteTests
{
    private static string SchemaRoot => Path.Combine(TestPaths.RepoRoot, "schemas", "adapter", "v1");

    [Fact]
    public async Task Fake_adapter_passes_the_full_conformance_suite()
    {
        await using var host = await FakeAdapterHost.StartAsync(FakeAdapterHostOptions.CreateDefault() with
        {
            UseRealNetworkServer = true,
            ControlPlanePort = 0
        });

        var report = await RunSuiteAsync(
            host.Client.BaseAddress!,
            scenarioId: "success",
            scenarioVersion: "1.0",
            protocol: "quic");

        Assert.Equal(AdapterConformanceOutcome.Passed, report.Outcome);
        Assert.Contains(report.Steps, step => step.Step == "health" && step.Outcome == AdapterConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "manifest" && step.Outcome == AdapterConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "invalid-transition" && step.Outcome == AdapterConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "delete-idempotent" && step.Outcome == AdapterConformanceOutcome.Passed);
    }

    [Fact]
    public async Task Unsupported_scenarios_remain_structured_results_not_harness_failures()
    {
        await using var host = await FakeAdapterHost.StartAsync(FakeAdapterHostOptions.CreateDefault() with
        {
            UseRealNetworkServer = true,
            ControlPlanePort = 0
        });

        var report = await RunSuiteAsync(
            host.Client.BaseAddress!,
            scenarioId: "unsupported",
            scenarioVersion: "1.0",
            protocol: "quic");

        Assert.Equal(AdapterConformanceOutcome.Unsupported, report.Outcome);
        Assert.Contains(report.Steps, step => step.Step == "prepare-unsupported" && step.Outcome == AdapterConformanceOutcome.Unsupported);
    }

    [Theory]
    [InlineData("prepare-failure")]
    [InlineData("start-failure")]
    public async Task Adapter_contract_failures_are_reported_as_contract_failures(string scenarioId)
    {
        await using var host = await FakeAdapterHost.StartAsync(FakeAdapterHostOptions.CreateDefault() with
        {
            UseRealNetworkServer = true,
            ControlPlanePort = 0
        });

        var report = await RunSuiteAsync(
            host.Client.BaseAddress!,
            scenarioId,
            scenarioVersion: "1.0",
            protocol: "quic");

        Assert.Equal(AdapterConformanceOutcome.ContractFailure, report.Outcome);
        Assert.Contains(report.Steps, step => step.Step is "prepare-failed" or "start-failed");
    }

    [Fact]
    public async Task Readiness_failures_are_distinguished_from_infrastructure_failures()
    {
        await using var protocolHost = await FakeAdapterHost.StartAsync(FakeAdapterHostOptions.CreateDefault() with
        {
            UseRealNetworkServer = true,
            ControlPlanePort = 0,
            ScenarioProfiles = new Dictionary<string, FakeAdapterScenarioProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["readiness-failure"] = FakeAdapterScenarioProfile.ReadinessFailure("readiness-failure")
            }
        });

        var report = await RunSuiteAsync(
            protocolHost.Client.BaseAddress!,
            scenarioId: "readiness-failure",
            scenarioVersion: "1.0",
            protocol: "quic");

        Assert.Equal(AdapterConformanceOutcome.Timeout, report.Outcome);
        Assert.Contains(report.Steps, step => step.Step == "readiness-not-ready" && step.Outcome == AdapterConformanceOutcome.Timeout);
    }

    [Fact]
    public async Task Timeout_probes_are_reported_as_timeouts_not_contract_failures()
    {
        await using var host = await FakeAdapterHost.StartAsync(FakeAdapterHostOptions.CreateDefault() with
        {
            UseRealNetworkServer = true,
            ControlPlanePort = 0,
            ResponseDelay = TimeSpan.FromMilliseconds(250)
        });

        var report = await RunSuiteAsync(
            host.Client.BaseAddress!,
            scenarioId: "success",
            scenarioVersion: "1.0",
            protocol: "quic",
            timeout: TimeSpan.FromMilliseconds(50));

        Assert.Equal(AdapterConformanceOutcome.Timeout, report.Outcome);
        Assert.Contains(report.Steps, step => step.Outcome == AdapterConformanceOutcome.Timeout);
    }

    [Fact]
    public async Task Problem_and_malformed_control_plane_responses_are_reported_explicitly()
    {
        var problemReport = await RunSuiteAsync(
            new Uri("http://fixture.local"),
            scenarioId: "success",
            scenarioVersion: "1.0",
            protocol: "quic",
            timeout: null,
            configureOptions: options => options.HttpMessageHandlerFactory = () => new ManifestProblemHandler());

        Assert.Equal(AdapterConformanceOutcome.ContractFailure, problemReport.Outcome);
        Assert.Contains(problemReport.Steps, step => step.Step == "manifest" && step.Outcome == AdapterConformanceOutcome.ContractFailure);
        Assert.Contains(problemReport.Steps, step => step.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable);

        var malformedReport = await RunSuiteAsync(
            new Uri("http://fixture.local"),
            scenarioId: "success",
            scenarioVersion: "1.0",
            protocol: "quic",
            timeout: null,
            configureOptions: options => options.HttpMessageHandlerFactory = () => new ManifestMalformedHandler());

        Assert.Equal(AdapterConformanceOutcome.MalformedResponse, malformedReport.Outcome);
        Assert.Contains(malformedReport.Steps, step => step.Step == "manifest" && step.Outcome == AdapterConformanceOutcome.MalformedResponse);
    }

    [Fact]
    public async Task Unsupported_contract_versions_short_circuit_as_structured_unsupported_results()
    {
        await using var host = await FakeAdapterHost.StartAsync(FakeAdapterHostOptions.CreateDefault() with
        {
            UseRealNetworkServer = true,
            ControlPlanePort = 0,
            ContractVersion = "v2"
        });

        var report = await RunSuiteAsync(
            host.Client.BaseAddress!,
            scenarioId: "success",
            scenarioVersion: "1.0",
            protocol: "quic");

        Assert.Equal(AdapterConformanceOutcome.Unsupported, report.Outcome);
        Assert.Contains(report.Steps, step => step.Step == "manifest-version" && step.Outcome == AdapterConformanceOutcome.Unsupported);
    }

    [Fact]
    public async Task Unreachable_control_planes_are_distinguished_as_infrastructure_failures()
    {
        var report = await RunSuiteAsync(
            new Uri("http://fixture.local"),
            scenarioId: "success",
            scenarioVersion: "1.0",
            protocol: "quic",
            timeout: TimeSpan.FromMilliseconds(500),
            configureOptions: options => options.HttpMessageHandlerFactory = () => new ThrowingHandler());

        Assert.Equal(AdapterConformanceOutcome.InfrastructureFailure, report.Outcome);
        Assert.Contains(report.Steps, step => step.Outcome == AdapterConformanceOutcome.InfrastructureFailure);
    }

    private static Task<AdapterConformanceReport> RunSuiteAsync(
        Uri controlPlaneBaseUrl,
        string scenarioId,
        string scenarioVersion,
        string protocol,
        TimeSpan? timeout = null)
    {
        return RunSuiteAsync(controlPlaneBaseUrl, scenarioId, scenarioVersion, protocol, timeout, null);
    }

    private static Task<AdapterConformanceReport> RunSuiteAsync(
        Uri controlPlaneBaseUrl,
        string scenarioId,
        string scenarioVersion,
        string protocol,
        TimeSpan? timeout,
        Action<AdapterConformanceOptions>? configureOptions)
    {
        var suite = new AdapterConformanceSuite();
        var options = new AdapterConformanceOptions
        {
            SchemaRootPath = SchemaRoot,
            SupportedContractVersion = "v1",
            Timeout = timeout ?? TimeSpan.FromSeconds(2)
        };
        configureOptions?.Invoke(options);

        return suite.RunAsync(
            controlPlaneBaseUrl,
            new AdapterConformanceScenario
            {
                ScenarioId = scenarioId,
                ScenarioVersion = scenarioVersion,
                Role = "server",
                Protocol = protocol,
                RunId = "adapter-conformance-test",
                CellId = "adapter-conformance-test",
                SessionLabel = "adapter-conformance-test",
                RequestedEndpointBindings =
                [
                    new AdapterEndpointBinding
                    {
                        BindingId = "primary",
                        Purpose = "test-endpoint",
                        EndpointType = protocol
                    }
                ],
                ArtifactOutputExpectations =
                [
                    new AdapterArtifactExpectation
                    {
                        ArtifactType = "log",
                        Required = true
                    }
                ]
            },
            options);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("fixture infrastructure failure");
        }
    }

    private sealed class ManifestProblemHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null)
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CreateJsonResponse(System.Net.HttpStatusCode.OK, new AdapterHealthResponse
                {
                    AdapterIdentity = new AdapterIdentity { Id = "fixture-adapter", Name = "Fixture Adapter" },
                    Status = AdapterHealthStatus.Ready,
                    VersionCompatibility = new AdapterVersionCompatibility { ContractVersion = "v1", CompatibleContractVersions = ["v1"] }
                }));
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/manifest", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CreateProblemResponse(System.Net.HttpStatusCode.ServiceUnavailable, "manifest-unavailable", "Fixture manifest unavailable."));
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }

    private sealed class ManifestMalformedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null)
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CreateJsonResponse(System.Net.HttpStatusCode.OK, new AdapterHealthResponse
                {
                    AdapterIdentity = new AdapterIdentity { Id = "fixture-adapter", Name = "Fixture Adapter" },
                    Status = AdapterHealthStatus.Ready,
                    VersionCompatibility = new AdapterVersionCompatibility { ContractVersion = "v1", CompatibleContractVersions = ["v1"] }
                }));
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/manifest", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"manifest\":", System.Text.Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }

    private static HttpResponseMessage CreateJsonResponse<T>(System.Net.HttpStatusCode statusCode, T payload)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, ProtocolLabAdapterJson.Options), System.Text.Encoding.UTF8)
            {
                Headers =
                {
                    ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
                }
            }
        };
    }

    private static HttpResponseMessage CreateProblemResponse(System.Net.HttpStatusCode statusCode, string code, string title)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(new AdapterProblemDetails
            {
                Type = "https://incursa.example/problems/fixture-adapter",
                Title = title,
                Status = (int)statusCode,
                Code = code,
                Operation = "manifest",
                Retryable = false
            }, ProtocolLabAdapterJson.Options), System.Text.Encoding.UTF8)
            {
                Headers =
                {
                    ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/problem+json")
                }
            }
        };
    }
}
