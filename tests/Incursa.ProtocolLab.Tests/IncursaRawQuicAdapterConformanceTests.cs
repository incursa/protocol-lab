// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Tests.Fixtures.IncursaRawQuicAdapterLab;

namespace Incursa.ProtocolLab.Tests;

[Collection(RunnerContractFixtureLabCollection.Name)]
public sealed class IncursaRawQuicAdapterConformanceTests
{
    [Fact]
    public async Task Adapter_reports_health_and_manifest()
    {
        await using var host = await IncursaRawQuicAdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);
        var health = await client.GetHealthAsync();
        Assert.Equal("incursa-raw-quic-adapter-v1", health.AdapterIdentity.Id);
        var manifest = await client.GetManifestAsync();
        Assert.Equal("incursa-raw-quic-adapter-v1", manifest.AdapterIdentity.Id);
        Assert.Equal("incursa-raw-quic", manifest.ImplementationIdentity.Id);
        Assert.Contains("quic.server", manifest.ClaimedCapabilities.Select(c => c.Id));
        Assert.Contains("quicTransport", manifest.ClaimedCapabilities.Select(c => c.Id));
    }

    [Fact]
    public async Task Adapter_reports_quic_endpoint_type()
    {
        await using var host = await IncursaRawQuicAdapterProcessHost.StartAsync();
        var manifest = await new ProtocolLabAdapterClient(host.Client).GetManifestAsync();
        var quic = manifest.SupportedEndpointTypes.FirstOrDefault(et => string.Equals(et.Type, "quic", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(quic);
        Assert.Contains("quic", quic!.Protocols);
        Assert.Equal("udp", quic.Extensions["transport"].GetString());
    }

    [Fact]
    public async Task Adapter_quic_endpoint_differs_from_control_plane()
    {
        await using var host = await IncursaRawQuicAdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);
        var s = await client.CreateSessionAsync(new AdapterSessionCreateRequest { RequestedSessionId = "incursa-raw-quic-ep-test", RunId = "test", CellId = "ep" });
        var p = await client.PrepareAsync(s.Session.SessionId, CreatePrepare("fixture.quic.handshake"));
        Assert.Equal(AdapterOperationResultCategory.Succeeded, p.Category);
        var start = await client.StartAsync(s.Session.SessionId);
        var ep = await client.GetEndpointsAsync(s.Session.SessionId);
        Assert.NotEmpty(ep.Endpoints);
        var e = ep.Endpoints[0];
        Assert.Equal("quic", e.Scheme); Assert.Equal("quic", e.Protocol); Assert.Equal("127.0.0.1", e.Host);
        Assert.NotEqual(host.Client.BaseAddress!.Port, e.Port);
        Assert.True(e.Extensions.ContainsKey("alpn")); Assert.Equal("udp", e.Extensions["transport"].GetString()); Assert.False(e.Extensions["datagramSupported"].GetBoolean());
        await client.StopAsync(s.Session.SessionId); await client.DeleteSessionAsync(s.Session.SessionId);
    }

    [Fact]
    public async Task Adapter_reports_unsupported_structurally()
    {
        await using var host = await IncursaRawQuicAdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);
        var s = await client.CreateSessionAsync(new AdapterSessionCreateRequest { RequestedSessionId = "incursa-raw-quic-unsup", RunId = "test", CellId = "unsup" });
        var p = await client.PrepareAsync(s.Session.SessionId, new AdapterPrepareRequest { ScenarioId = "fixture.quic.unsupported", ScenarioVersion = "1.0", Role = "server", ScenarioDocument = JsonSerializer.SerializeToElement(new { id = "fixture.quic.unsupported", version = "1.0", protocol = "quic", implementationRole = "server", family = "fixture.quic", requiredCapabilities = new[] { "quicTransport", "quicDatagram" } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), RequestedEndpointBindings = [new AdapterEndpointBinding { BindingId = "primary", Purpose = "test", EndpointType = "quic" }] });
        Assert.Equal(AdapterOperationResultCategory.Unsupported, p.Category);
        await client.StopAsync(s.Session.SessionId); await client.DeleteSessionAsync(s.Session.SessionId);
    }

    [Fact]
    public async Task Adapter_rejects_non_quic_protocol()
    {
        await using var host = await IncursaRawQuicAdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);
        var s = await client.CreateSessionAsync(new AdapterSessionCreateRequest { RequestedSessionId = "incursa-raw-quic-nq", RunId = "test", CellId = "proto-rej" });
        var p = await client.PrepareAsync(s.Session.SessionId, new AdapterPrepareRequest { ScenarioId = "fixture.quic.handshake", ScenarioVersion = "1.0", Role = "server", ScenarioDocument = JsonSerializer.SerializeToElement(new { id = "fixture.quic.handshake", version = "1.0", protocol = "h3", implementationRole = "server", family = "fixture.quic", requiredCapabilities = new[] { "quicTransport", "quicHandshake" } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), RequestedEndpointBindings = [new AdapterEndpointBinding { BindingId = "primary", Purpose = "test", EndpointType = "h3" }] });
        Assert.Equal(AdapterOperationResultCategory.Unsupported, p.Category);
        await client.DeleteSessionAsync(s.Session.SessionId);
    }

    [Fact]
    public async Task Adapter_cleanup_works_on_success()
    {
        await using var host = await IncursaRawQuicAdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);
        var s = await client.CreateSessionAsync(new AdapterSessionCreateRequest { RunId = "irq-cleanup", CellId = "ok" });
        await client.PrepareAsync(s.Session.SessionId, CreatePrepare("fixture.quic.handshake"));
        Assert.NotNull(await client.StopAsync(s.Session.SessionId));
        Assert.NotNull(await client.DeleteSessionAsync(s.Session.SessionId));
        Assert.NotNull(await client.DeleteSessionAsync(s.Session.SessionId));
    }

    [Fact]
    public async Task Adapter_cleanup_works_on_unsupported()
    {
        await using var host = await IncursaRawQuicAdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);
        var s = await client.CreateSessionAsync(new AdapterSessionCreateRequest { RunId = "irq-cleanup", CellId = "unsup" });
        await client.PrepareAsync(s.Session.SessionId, new AdapterPrepareRequest { ScenarioId = "fixture.quic.unsupported", ScenarioVersion = "1.0", Role = "server", ScenarioDocument = JsonSerializer.SerializeToElement(new { id = "fixture.quic.unsupported", version = "1.0", protocol = "quic", implementationRole = "server", family = "fixture.quic", requiredCapabilities = new[] { "quicTransport", "quicDatagram" } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), RequestedEndpointBindings = [new AdapterEndpointBinding { BindingId = "primary", Purpose = "test", EndpointType = "quic" }] });
        Assert.NotNull(await client.StopAsync(s.Session.SessionId));
        Assert.NotNull(await client.DeleteSessionAsync(s.Session.SessionId));
    }

    [Fact]
    public async Task Adapter_metrics_and_artifacts_available()
    {
        await using var host = await IncursaRawQuicAdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);
        var s = await client.CreateSessionAsync(new AdapterSessionCreateRequest { RunId = "irq-metrics", CellId = "ma" });
        await client.PrepareAsync(s.Session.SessionId, CreatePrepare("fixture.quic.handshake"));
        var m = await client.GetMetricsAsync(s.Session.SessionId); Assert.NotNull(m);
        var a = await client.GetArtifactsAsync(s.Session.SessionId); Assert.NotNull(a); Assert.NotEmpty(a.Artifacts);
        await client.StopAsync(s.Session.SessionId); await client.DeleteSessionAsync(s.Session.SessionId);
    }

    [Fact]
    public async Task Adapter_delete_is_idempotent()
    {
        await using var host = await IncursaRawQuicAdapterProcessHost.StartAsync();
        var client = new ProtocolLabAdapterClient(host.Client);
        var s = await client.CreateSessionAsync(new AdapterSessionCreateRequest { RunId = "irq-idem", CellId = "idem" });
        await client.PrepareAsync(s.Session.SessionId, new AdapterPrepareRequest { ScenarioId = "fixture.quic.unsupported", ScenarioVersion = "1.0", Role = "server", ScenarioDocument = JsonSerializer.SerializeToElement(new { id = "fixture.quic.unsupported", version = "1.0", protocol = "quic", implementationRole = "server", family = "fixture.quic", requiredCapabilities = new[] { "quicTransport", "quicDatagram" } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), RequestedEndpointBindings = [new AdapterEndpointBinding { BindingId = "primary", Purpose = "test", EndpointType = "quic" }] });
        await client.StopAsync(s.Session.SessionId); await client.DeleteSessionAsync(s.Session.SessionId);
        Assert.NotNull(await client.DeleteSessionAsync(s.Session.SessionId));
    }

    [Fact]
    public async Task Incursa_and_msquic_declare_compatible_initial_capabilities()
    {
        await using var incursa = await IncursaRawQuicAdapterProcessHost.StartAsync();
        var iManifest = await new ProtocolLabAdapterClient(incursa.Client).GetManifestAsync();
        var iCaps = iManifest.ClaimedCapabilities.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("quic.server", iCaps); Assert.Contains("quicTransport", iCaps); Assert.Contains("quicHandshake", iCaps); Assert.Contains("quicStreams", iCaps);

        await using var msquic = await Fixtures.MsQuicDotNetAdapterLab.MsQuicDotNetAdapterProcessHost.StartAsync(new Fixtures.MsQuicDotNetAdapterLab.MsQuicDotNetAdapterProcessOptions { ControlPlanePort = 53382 });
        var mManifest = await new ProtocolLabAdapterClient(msquic.Client).GetManifestAsync();
        var mCaps = mManifest.ClaimedCapabilities.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("quic.server", mCaps); Assert.Contains("quicTransport", mCaps); Assert.Contains("quicHandshake", mCaps); Assert.Contains("quicStreams", mCaps);

        var quicRelated = new[] { "quicTransport", "quicHandshake", "quicStreams" };
        foreach (var cap in quicRelated)
        {
            Assert.Contains(cap, iCaps);
            Assert.Contains(cap, mCaps);
        }
    }

    private static AdapterPrepareRequest CreatePrepare(string scenarioId) => new()
    {
        ScenarioId = scenarioId, ScenarioVersion = "1.0", Role = "server",
        ScenarioDocument = JsonSerializer.SerializeToElement(new { id = scenarioId, version = "1.0", protocol = "quic", implementationRole = "server", family = "fixture.quic", requiredCapabilities = new[] { "quicTransport", "quicHandshake" }, quicTransport = new { behavior = "handshake-fixture", connectionCount = 1, streamType = "none", streamCount = 0, payloadDirection = "none", openPattern = "sequential", expectedBytes = 0 } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        RequestedEndpointBindings = [new AdapterEndpointBinding { BindingId = "primary", Purpose = "test-endpoint", EndpointType = "quic" }]
    };
}
