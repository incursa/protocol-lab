// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Tests.Fixtures.MsQuicDotNetAdapterLab;

namespace Incursa.ProtocolLab.Tests;

[Collection(RunnerContractFixtureLabCollection.Name)]
public sealed class MsQuicDotNetAdapterConformanceTests
{
    [Fact]
    public async Task Adapter_reports_health_and_manifest()
    {
        await using var host = await MsQuicDotNetAdapterProcessHost.StartAsync();

        var client = new ProtocolLabAdapterClient(host.Client);
        var health = await client.GetHealthAsync();
        Assert.NotNull(health);
        Assert.Equal("msquic-dotnet-raw-adapter-v1", health.AdapterIdentity.Id);

        var manifest = await client.GetManifestAsync();
        Assert.NotNull(manifest);
        Assert.Equal("msquic-dotnet-raw-adapter-v1", manifest.AdapterIdentity.Id);
        Assert.Equal("msquic-dotnet-raw", manifest.ImplementationIdentity.Id);
        Assert.Contains("quic.server", manifest.ClaimedCapabilities.Select(c => c.Id));
        Assert.Contains("quicTransport", manifest.ClaimedCapabilities.Select(c => c.Id));
        Assert.Contains("quicHandshake", manifest.ClaimedCapabilities.Select(c => c.Id));
        Assert.Contains("quicStreams", manifest.ClaimedCapabilities.Select(c => c.Id));
    }

    [Fact]
    public async Task Adapter_reports_quic_endpoint_type()
    {
        await using var host = await MsQuicDotNetAdapterProcessHost.StartAsync();

        var client = new ProtocolLabAdapterClient(host.Client);
        var manifest = await client.GetManifestAsync();

        var quicEndpointType = manifest.SupportedEndpointTypes
            .FirstOrDefault(et => string.Equals(et.Type, "quic", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(quicEndpointType);
        Assert.Contains("quic", quicEndpointType!.Protocols);
        Assert.True(quicEndpointType.Extensions.ContainsKey("transport"));
        Assert.Equal("udp", quicEndpointType.Extensions["transport"].GetString());
        Assert.True(quicEndpointType.Extensions.ContainsKey("alpn"));
    }

    [Fact]
    public async Task Adapter_control_plane_url_differs_from_quic_protocol_endpoint()
    {
        await using var host = await MsQuicDotNetAdapterProcessHost.StartAsync();

        var client = new ProtocolLabAdapterClient(host.Client);

        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = "msquic-adapter-endpoint-test",
            RunId = "msquic-adapter-test",
            CellId = "endpoint-separation"
        });

        await client.PrepareAsync(session.Session.SessionId, CreatePrepareRequest("fixture.quic.handshake"));

        var start = await client.StartAsync(session.Session.SessionId);

        var endpoints = await client.GetEndpointsAsync(session.Session.SessionId);
        Assert.NotEmpty(endpoints.Endpoints);

        var protocolEndpoint = endpoints.Endpoints[0];
        var controlPlaneUrl = host.Client.BaseAddress!.ToString().TrimEnd('/');

        Assert.NotEqual(controlPlaneUrl, $"{protocolEndpoint.Scheme}://{protocolEndpoint.Host}:{protocolEndpoint.Port}");
        Assert.Equal("quic", protocolEndpoint.Scheme);
        Assert.Equal("quic", protocolEndpoint.Protocol);
        Assert.Equal("127.0.0.1", protocolEndpoint.Host);
        Assert.True(protocolEndpoint.Extensions.ContainsKey("alpn"));
        Assert.True(protocolEndpoint.Extensions.ContainsKey("transport"));
        Assert.Equal("udp", protocolEndpoint.Extensions["transport"].GetString());
        Assert.True(protocolEndpoint.Extensions.ContainsKey("streamBehavior"));
        Assert.True(protocolEndpoint.Extensions.ContainsKey("datagramSupported"));
        Assert.False(protocolEndpoint.Extensions["datagramSupported"].GetBoolean());
        Assert.True(protocolEndpoint.Extensions.ContainsKey("zeroRttSupported"));
        Assert.False(protocolEndpoint.Extensions["zeroRttSupported"].GetBoolean());

        await client.StopAsync(session.Session.SessionId);
        await client.DeleteSessionAsync(session.Session.SessionId);
    }

    [Fact]
    public async Task Adapter_reports_unsupported_scenario_structurally()
    {
        await using var host = await MsQuicDotNetAdapterProcessHost.StartAsync();

        var client = new ProtocolLabAdapterClient(host.Client);

        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = "msquic-adapter-unsupported-test",
            RunId = "msquic-adapter-test",
            CellId = "unsupported"
        });

        var prepare = await client.PrepareAsync(session.Session.SessionId, new AdapterPrepareRequest
        {
            ScenarioId = "fixture.quic.unsupported",
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = JsonSerializer.SerializeToElement(new
            {
                id = "fixture.quic.unsupported",
                version = "1.0",
                protocol = "quic",
                implementationRole = "server",
                family = "fixture.quic",
                requiredCapabilities = new[] { "quicTransport", "quicDatagram" }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            RequestedEndpointBindings =
            [
                new AdapterEndpointBinding
                {
                    BindingId = "primary",
                    Purpose = "test-endpoint",
                    EndpointType = "quic"
                }
            ]
        });

        Assert.Equal(AdapterOperationResultCategory.Unsupported, prepare.Category);

        await client.StopAsync(session.Session.SessionId);
        await client.DeleteSessionAsync(session.Session.SessionId);
    }

    [Fact]
    public async Task Adapter_rejects_non_quic_protocol()
    {
        await using var host = await MsQuicDotNetAdapterProcessHost.StartAsync();

        var client = new ProtocolLabAdapterClient(host.Client);

        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RequestedSessionId = "msquic-adapter-protocol-test",
            RunId = "msquic-adapter-test",
            CellId = "protocol-rejection"
        });

        var prepare = await client.PrepareAsync(session.Session.SessionId, new AdapterPrepareRequest
        {
            ScenarioId = "fixture.quic.handshake",
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = JsonSerializer.SerializeToElement(new
            {
                id = "fixture.quic.handshake",
                version = "1.0",
                protocol = "h3",
                implementationRole = "server",
                family = "fixture.quic",
                requiredCapabilities = new[] { "quicTransport", "quicHandshake" }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            RequestedEndpointBindings =
            [
                new AdapterEndpointBinding
                {
                    BindingId = "primary",
                    Purpose = "test-endpoint",
                    EndpointType = "h3"
                }
            ]
        });

        Assert.Equal(AdapterOperationResultCategory.Unsupported, prepare.Category);

        await client.DeleteSessionAsync(session.Session.SessionId);
    }

    [Fact]
    public async Task Adapter_cleanup_works_on_success()
    {
        await using var host = await MsQuicDotNetAdapterProcessHost.StartAsync();

        var client = new ProtocolLabAdapterClient(host.Client);

        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RunId = "msquic-adapter-cleanup-test",
            CellId = "cleanup-success"
        });

        await client.PrepareAsync(session.Session.SessionId, CreatePrepareRequest("fixture.quic.handshake"));

        var stop = await client.StopAsync(session.Session.SessionId);
        Assert.NotNull(stop);

        var delete = await client.DeleteSessionAsync(session.Session.SessionId);
        Assert.NotNull(delete);

        var deleteAgain = await client.DeleteSessionAsync(session.Session.SessionId);
        Assert.NotNull(deleteAgain);
    }

    [Fact]
    public async Task Adapter_cleanup_works_on_unsupported_session()
    {
        await using var host = await MsQuicDotNetAdapterProcessHost.StartAsync();

        var client = new ProtocolLabAdapterClient(host.Client);

        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RunId = "msquic-adapter-cleanup-unsupported",
            CellId = "cleanup-unsupported"
        });

        await client.PrepareAsync(session.Session.SessionId, new AdapterPrepareRequest
        {
            ScenarioId = "fixture.quic.unsupported",
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = JsonSerializer.SerializeToElement(new
            {
                id = "fixture.quic.unsupported",
                version = "1.0",
                protocol = "quic",
                implementationRole = "server",
                family = "fixture.quic",
                requiredCapabilities = new[] { "quicTransport", "quicDatagram" }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            RequestedEndpointBindings =
            [
                new AdapterEndpointBinding
                {
                    BindingId = "primary",
                    Purpose = "test-endpoint",
                    EndpointType = "quic"
                }
            ]
        });

        var stop = await client.StopAsync(session.Session.SessionId);
        Assert.NotNull(stop);

        var delete = await client.DeleteSessionAsync(session.Session.SessionId);
        Assert.NotNull(delete);
    }

    [Fact]
    public async Task Adapter_metrics_and_artifacts_are_available()
    {
        await using var host = await MsQuicDotNetAdapterProcessHost.StartAsync();

        var client = new ProtocolLabAdapterClient(host.Client);

        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RunId = "msquic-adapter-metrics-test",
            CellId = "metrics-artifacts"
        });

        await client.PrepareAsync(session.Session.SessionId, CreatePrepareRequest("fixture.quic.handshake"));

        var metrics = await client.GetMetricsAsync(session.Session.SessionId);
        Assert.NotNull(metrics);

        var artifacts = await client.GetArtifactsAsync(session.Session.SessionId);
        Assert.NotNull(artifacts);
        Assert.NotEmpty(artifacts.Artifacts);

        await client.StopAsync(session.Session.SessionId);
        await client.DeleteSessionAsync(session.Session.SessionId);
    }

    [Fact]
    public async Task Adapter_delete_is_idempotent()
    {
        await using var host = await MsQuicDotNetAdapterProcessHost.StartAsync();

        var client = new ProtocolLabAdapterClient(host.Client);

        var session = await client.CreateSessionAsync(new AdapterSessionCreateRequest
        {
            RunId = "msquic-adapter-idempotent-test",
            CellId = "idempotent"
        });

        await client.PrepareAsync(session.Session.SessionId, new AdapterPrepareRequest
        {
            ScenarioId = "fixture.quic.unsupported",
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = JsonSerializer.SerializeToElement(new
            {
                id = "fixture.quic.unsupported",
                version = "1.0",
                protocol = "quic",
                implementationRole = "server",
                family = "fixture.quic",
                requiredCapabilities = new[] { "quicTransport", "quicDatagram" }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            RequestedEndpointBindings =
            [
                new AdapterEndpointBinding
                {
                    BindingId = "primary",
                    Purpose = "test-endpoint",
                    EndpointType = "quic"
                }
            ]
        });

        await client.StopAsync(session.Session.SessionId);
        await client.DeleteSessionAsync(session.Session.SessionId);

        var deleteAgain = await client.DeleteSessionAsync(session.Session.SessionId);
        Assert.NotNull(deleteAgain);
    }

    private static AdapterPrepareRequest CreatePrepareRequest(string scenarioId)
    {
        return new AdapterPrepareRequest
        {
            ScenarioId = scenarioId,
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = JsonSerializer.SerializeToElement(new
            {
                id = scenarioId,
                version = "1.0",
                protocol = "quic",
                implementationRole = "server",
                family = "fixture.quic",
                requiredCapabilities = new[] { "quicTransport", "quicHandshake" },
                quicTransport = new
                {
                    behavior = "handshake-fixture",
                    connectionCount = 1,
                    streamType = "none",
                    streamCount = 0,
                    payloadDirection = "none",
                    openPattern = "sequential",
                    expectedBytes = 0
                }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            RequestedEndpointBindings =
            [
                new AdapterEndpointBinding
                {
                    BindingId = "primary",
                    Purpose = "test-endpoint",
                    EndpointType = "quic"
                }
            ]
        };
    }
}
