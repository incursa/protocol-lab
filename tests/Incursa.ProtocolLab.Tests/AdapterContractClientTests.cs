// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Tests.Fixtures.AdapterContractLab;

namespace Incursa.ProtocolLab.Tests;

public sealed class AdapterContractClientTests
{
    [Fact]
    public async Task Fixture_successful_lifecycle_discovers_quic_endpoints_metrics_and_artifacts()
    {
        await using var host = await FakeAdapterHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);

        var health = await client.GetHealthAsync();
        Assert.Equal(AdapterHealthStatus.Ready, health.Status);
        Assert.Equal("fixture-adapter", health.AdapterIdentity.Id);

        var manifest = await client.GetManifestAsync();
        Assert.Contains("server", manifest.SupportedRoles);
        Assert.Contains(manifest.SupportedEndpointTypes, endpointType => endpointType.Type == "quic");
        Assert.Equal("http", host.Client.BaseAddress!.Scheme);

        using var scenarioDocument = JsonDocument.Parse("""{"purpose":"fixture"}""");
        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = "session-001",
            RunId = "run-001",
            CellId = "cell-001"
        });

        Assert.Equal("session-001", session.Session.SessionId);
        Assert.Equal(AdapterSessionState.Created, session.Session.State);

        var prepare = await client.PrepareAsync("session-001", new AdapterPrepareRequest
        {
            ScenarioId = "success",
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = scenarioDocument.RootElement.Clone(),
            RequestedEndpointBindings =
            [
                new AdapterEndpointBinding
                {
                    BindingId = "primary",
                    Purpose = "test-endpoint",
                    EndpointType = "quic"
                }
            ],
            RunId = "run-001",
            CellId = "cell-001",
            ArtifactOutputExpectations =
            [
                new AdapterArtifactExpectation
                {
                    ArtifactType = "log",
                    Required = true
                }
            ]
        });

        Assert.Equal(AdapterOperationResultCategory.Succeeded, prepare.Category);

        var start = await client.StartAsync("session-001");
        Assert.Equal(AdapterOperationResultCategory.Succeeded, start.Category);

        var status = await client.GetStatusAsync("session-001");
        Assert.Equal(AdapterSessionState.Running, status.Session.State);
        Assert.Equal(AdapterReadinessStatus.Ready, status.Readiness.Status);
        Assert.Equal(AdapterHealthStatus.Ready, status.Health.Status);

        var endpoints = await client.GetEndpointsAsync("session-001");
        var endpoint = Assert.Single(endpoints.Endpoints);
        Assert.Equal("quic", endpoint.Scheme);
        Assert.Equal("quic", endpoint.Protocol);
        Assert.Equal(4433, endpoint.Port);
        Assert.Equal("fixture-quic", endpoint.Tls!.Sni);

        var metrics = await client.GetMetricsAsync("session-001");
        Assert.Equal(AdapterResourceAvailability.Available, metrics.Availability);
        Assert.Contains(metrics.Metrics, metric => metric.MetricId == "fixture.metric.requests");

        var artifacts = await client.GetArtifactsAsync("session-001");
        Assert.Equal(AdapterResourceAvailability.Available, artifacts.Availability);
        Assert.Contains(artifacts.Artifacts, artifact => artifact.ArtifactType == "log");

        var stop = await client.StopAsync("session-001");
        Assert.Equal(AdapterOperationResultCategory.Succeeded, stop.Category);

        var delete = await client.DeleteSessionAsync("session-001");
        Assert.Equal(AdapterOperationResultCategory.Succeeded, delete.Category);

        var ex = await Assert.ThrowsAsync<ProtocolLabAdapterProblemException>(() => client.GetSessionAsync("session-001"));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("session-not-found", ex.Problem.Code);
    }

    [Fact]
    public async Task Fixture_unsupported_scenario_returns_structured_unsupported_results()
    {
        await using var host = await FakeAdapterHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);

        await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = "session-unsupported",
            RunId = "run-unsupported",
            CellId = "cell-unsupported"
        });

        using var scenarioDocument = JsonDocument.Parse("""{"scenario":"unsupported"}""");
        var prepare = await client.PrepareAsync("session-unsupported", new AdapterPrepareRequest
        {
            ScenarioId = "unsupported",
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = scenarioDocument.RootElement.Clone(),
            RequestedEndpointBindings = [],
            RunId = "run-unsupported",
            CellId = "cell-unsupported",
            ArtifactOutputExpectations = []
        });

        Assert.Equal(AdapterOperationResultCategory.Unsupported, prepare.Category);

        var status = await client.GetStatusAsync("session-unsupported");
        Assert.Equal(AdapterSessionState.Unsupported, status.Session.State);
        Assert.Equal(AdapterReadinessStatus.Unsupported, status.Readiness.Status);

        var endpoints = await client.GetEndpointsAsync("session-unsupported");
        Assert.Empty(endpoints.Endpoints);
        Assert.Equal(AdapterOperationResultCategory.Unsupported, endpoints.Operation!.Category);

        var metrics = await client.GetMetricsAsync("session-unsupported");
        Assert.Equal(AdapterResourceAvailability.Unsupported, metrics.Availability);
        Assert.Equal(AdapterOperationResultCategory.Unsupported, metrics.Operation!.Category);

        var artifacts = await client.GetArtifactsAsync("session-unsupported");
        Assert.Equal(AdapterResourceAvailability.Unsupported, artifacts.Availability);
        Assert.Equal(AdapterOperationResultCategory.Unsupported, artifacts.Operation!.Category);
    }

    [Fact]
    public async Task Fixture_prepare_failure_is_reported_as_structured_failure()
    {
        await using var host = await FakeAdapterHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);

        await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = "session-prepare-failure",
            RunId = "run-prepare-failure",
            CellId = "cell-prepare-failure"
        });

        using var scenarioDocument = JsonDocument.Parse("""{"scenario":"prepare-failure"}""");
        var prepare = await client.PrepareAsync("session-prepare-failure", new AdapterPrepareRequest
        {
            ScenarioId = "prepare-failure",
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = scenarioDocument.RootElement.Clone(),
            RequestedEndpointBindings = [],
            RunId = "run-prepare-failure",
            CellId = "cell-prepare-failure",
            ArtifactOutputExpectations = []
        });

        Assert.Equal(AdapterOperationResultCategory.Failed, prepare.Category);

        var status = await client.GetStatusAsync("session-prepare-failure");
        Assert.Equal(AdapterSessionState.Failed, status.Session.State);
        Assert.Equal(AdapterReadinessStatus.Failed, status.Readiness.Status);
    }

    [Fact]
    public async Task Fixture_start_failure_is_reported_as_structured_failure()
    {
        await using var host = await FakeAdapterHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);

        await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = "session-start-failure",
            RunId = "run-start-failure",
            CellId = "cell-start-failure"
        });

        using var scenarioDocument = JsonDocument.Parse("""{"scenario":"start-failure"}""");
        var prepare = await client.PrepareAsync("session-start-failure", new AdapterPrepareRequest
        {
            ScenarioId = "start-failure",
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = scenarioDocument.RootElement.Clone(),
            RequestedEndpointBindings = [],
            RunId = "run-start-failure",
            CellId = "cell-start-failure",
            ArtifactOutputExpectations = []
        });

        Assert.Equal(AdapterOperationResultCategory.Succeeded, prepare.Category);

        var start = await client.StartAsync("session-start-failure");
        Assert.Equal(AdapterOperationResultCategory.Failed, start.Category);

        var status = await client.GetStatusAsync("session-start-failure");
        Assert.Equal(AdapterSessionState.Failed, status.Session.State);
        Assert.Equal(AdapterReadinessStatus.Failed, status.Readiness.Status);
    }

    [Fact]
    public async Task Fixture_readiness_failure_stays_not_ready_without_becoming_an_infrastructure_error()
    {
        await using var host = await FakeAdapterHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);

        await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = "session-readiness-failure",
            RunId = "run-readiness-failure",
            CellId = "cell-readiness-failure"
        });

        using var scenarioDocument = JsonDocument.Parse("""{"scenario":"readiness-failure"}""");
        var prepare = await client.PrepareAsync("session-readiness-failure", new AdapterPrepareRequest
        {
            ScenarioId = "readiness-failure",
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = scenarioDocument.RootElement.Clone(),
            RequestedEndpointBindings = [],
            RunId = "run-readiness-failure",
            CellId = "cell-readiness-failure",
            ArtifactOutputExpectations = []
        });

        Assert.Equal(AdapterOperationResultCategory.Succeeded, prepare.Category);

        var start = await client.StartAsync("session-readiness-failure");
        Assert.Equal(AdapterOperationResultCategory.Succeeded, start.Category);

        var status = await client.GetStatusAsync("session-readiness-failure");
        Assert.Equal(AdapterSessionState.Running, status.Session.State);
        Assert.Equal(AdapterReadinessStatus.NotReady, status.Readiness.Status);
        Assert.Contains("not ready", status.Readiness.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fixture_problem_and_malformed_responses_are_reported_by_the_client()
    {
        await using var problemHost = await FakeAdapterHost.StartAsync(FakeAdapterHostOptions.CreateDefault() with
        {
            ManifestBehavior = FakeAdapterManifestBehavior.Problem
        });

        var problemClient = new ProtocolLabAdapterClient(problemHost.Client);
        var problem = await Assert.ThrowsAsync<ProtocolLabAdapterProblemException>(() => problemClient.GetManifestAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, problem.StatusCode);
        Assert.Equal("manifest-unavailable", problem.Problem.Code);

        await using var malformedHost = await FakeAdapterHost.StartAsync(FakeAdapterHostOptions.CreateDefault() with
        {
            ManifestBehavior = FakeAdapterManifestBehavior.Malformed
        });

        var malformedClient = new ProtocolLabAdapterClient(malformedHost.Client);
        var malformed = await Assert.ThrowsAsync<ProtocolLabAdapterProtocolException>(() => malformedClient.GetManifestAsync());
        Assert.Equal(HttpStatusCode.OK, malformed.StatusCode);
        Assert.Contains("{\"manifest\":", malformed.RawContent, StringComparison.OrdinalIgnoreCase);
    }
}
