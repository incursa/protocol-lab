// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class ScenarioParsingTests
{
    [Fact]
    public void Parses_plaintext_scenario()
    {
        var scenario = YamlFile.Load<ScenarioDefinition>(
            Path.Combine(TestPaths.RepoRoot, "scenarios", "http", "core", "plaintext.yaml"));

        Assert.Equal("http.core.plaintext", scenario.Id);
        Assert.Equal("http.application", scenario.Family);
        Assert.Equal("h1", scenario.Protocol);
        Assert.Equal("1.0", scenario.SchemaVersion);
        Assert.Equal("stable", scenario.Status);
        Assert.Equal("workload", scenario.Kind);
        Assert.Equal("application", scenario.Layer);
        Assert.Equal("request-response", scenario.TrafficShape);
        Assert.NotEmpty(scenario.Roles);
        Assert.Equal("HTTP Plaintext", scenario.GetTitle());
        Assert.NotNull(scenario.Endpoint);
        Assert.Equal("/plaintext", scenario.Endpoint.Path);
        Assert.Equal("exact", scenario.Endpoint.ExpectedBodyRule);
        Assert.Equal("Hello, World!", scenario.Endpoint.ExpectedBody);
        Assert.NotNull(scenario.Requires);
        Assert.NotEmpty(scenario.Requires.Protocols);
        Assert.Contains("h1", scenario.GetEffectiveProtocols());
        Assert.Contains("h2", scenario.GetEffectiveProtocols());
        Assert.Contains("h3", scenario.GetEffectiveProtocols());
        Assert.NotNull(scenario.Artifacts);
        Assert.Contains("validation.json", scenario.Artifacts.Required);
        Assert.Contains("result.json", scenario.Artifacts.Required);
        Assert.Contains("load-tool.stdout.txt", scenario.Artifacts.Required);
        Assert.Contains("load-tool.stderr.txt", scenario.Artifacts.Required);
    }

    [Fact]
    public void Http1_core_smoke_scenarios_are_protocol_neutral_http_application_tests()
    {
        var scenarios = new[]
        {
            YamlFile.Load<ScenarioDefinition>(Path.Combine(TestPaths.RepoRoot, "scenarios", "http", "core", "plaintext.yaml")),
            YamlFile.Load<ScenarioDefinition>(Path.Combine(TestPaths.RepoRoot, "scenarios", "http", "core", "json.yaml")),
            YamlFile.Load<ScenarioDefinition>(Path.Combine(TestPaths.RepoRoot, "scenarios", "http", "payload", "bytes-1kb.yaml"))
        };

        Assert.All(scenarios, scenario =>
        {
            Assert.Equal("h1", scenario.Protocol);
            Assert.Equal("http.application", scenario.Family);
            Assert.Equal("server", scenario.ImplementationRole);
            Assert.NotNull(scenario.Endpoint);
            Assert.True(scenario.Validation.Required);
            Assert.Contains("h1", scenario.GetEffectiveProtocols());
            Assert.Contains("h2", scenario.GetEffectiveProtocols());
            Assert.Contains("h3", scenario.GetEffectiveProtocols());
            Assert.Contains("validation.json", scenario.Artifacts.Required);
            Assert.Contains("result.json", scenario.Artifacts.Required);
            Assert.Contains("load-tool.stdout.txt", scenario.Artifacts.Required);
            Assert.Contains("load-tool.stderr.txt", scenario.Artifacts.Required);
            Assert.Contains("requestsPerSecond", scenario.BenchmarkCompat!.PrimaryMetrics);
            Assert.Contains("smoke", scenario.BenchmarkCompat.CompatibleLoadShapes);
        });
    }

    [Fact]
    public void Placeholder_scenario_is_not_runnable_by_default()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "websocket.echo",
            Status = "placeholder",
            Protocol = "ws",
            Family = "websocket",
            ImplementationRole = "server"
        };

        Assert.True(scenario.IsPlaceholder());
        Assert.False(scenario.IsExperimental());
        Assert.False(scenario.IsStable());
    }

    [Fact]
    public void Experimental_scenario_requires_opt_in()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "http3.settings",
            Status = "experimental",
            Protocol = "h3",
            Family = "h3.protocol",
            ImplementationRole = "server"
        };

        Assert.True(scenario.IsExperimental());
        Assert.False(scenario.IsPlaceholder());
        Assert.False(scenario.IsStable());
    }

    [Fact]
    public void Stable_scenario_has_default_status()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "http.plaintext",
            Status = "stable",
            Protocol = "h3",
            Family = "http.application"
        };

        Assert.True(scenario.IsStable());
        Assert.False(scenario.IsExperimental());
        Assert.False(scenario.IsPlaceholder());
    }

    [Fact]
    public void Scenario_with_empty_status_is_stable()
    {
        var scenario = new ScenarioDefinition
        {
            Id = "http.test",
            Protocol = "h3",
            Family = "http.application"
        };

        Assert.True(scenario.IsStable());
    }

    [Fact]
    public void Loads_scenarios_from_catalog()
    {
        var scenarios = ScenarioCatalog.Load(Path.Combine(TestPaths.RepoRoot, "scenarios"));

        Assert.Equal(25, scenarios.Count);
        Assert.Contains(scenarios, scenario => scenario.Id == "http.core.plaintext");
        Assert.Contains(scenarios, scenario => scenario.Id == "http.core.json");
        Assert.Contains(scenarios, scenario => scenario.Id == "http.payload.bytes.1mb");
        Assert.Contains(scenarios, scenario => scenario.Id == "http.upload.hash.1mb");
        Assert.Contains(scenarios, scenario => scenario.Id == "http.headers.inspect-request");
        Assert.Contains(scenarios, scenario => scenario.Id == "h3.protocol.qpack-repeated-headers");
        Assert.Contains(scenarios, scenario => scenario.Id == "h3.protocol.cancel-mid-response");
        Assert.Contains(scenarios, scenario => scenario.Id == "h3.protocol.multiplex-100-streams");
        Assert.Contains(scenarios, scenario => scenario.Id == "quic.transport.handshake-cold");
        Assert.Contains(scenarios, scenario => scenario.Id == "quic.transport.stream-throughput.1mb");
        Assert.Contains(scenarios, scenario => scenario.Id == "quic.transport.multiplex.100x64kb");
        Assert.Contains(scenarios, scenario => scenario.Id == "quic.transport.connection-churn");
        Assert.Contains(scenarios, scenario => scenario.Id == "quic.transport.duplex-streams");
        Assert.Contains(scenarios, scenario => scenario.Id == "webtransport.session-bidi-echo");
        Assert.Contains(scenarios, scenario => scenario.Id == "masque.connect-udp-tunnel");
        Assert.Contains(scenarios, scenario => scenario.Id == "websocket.echo");
        Assert.Contains(scenarios, scenario => scenario.Id == "webtransport.session-open");
        Assert.Contains(scenarios, scenario => scenario.Id == "masque.connect-udp");
    }

    [Fact]
    public void Parses_h3_protocol_scenario_without_endpoint_contract()
    {
        var scenario = YamlFile.Load<ScenarioDefinition>(
            Path.Combine(TestPaths.RepoRoot, "scenarios", "h3", "protocol", "multiplex-100-streams.yaml"));

        Assert.Equal("h3.protocol.multiplex-100-streams", scenario.Id);
        Assert.Equal("h3.protocol", scenario.Family);
        Assert.Equal("h3", scenario.Protocol);
        Assert.Null(scenario.Endpoint);
        Assert.NotNull(scenario.H3Protocol);
        Assert.Equal("multiplex-request-streams", scenario.H3Protocol.Behavior);
        Assert.Equal(100, scenario.H3Protocol.ConcurrentRequestStreams);
        Assert.Contains("h3Multiplexing", scenario.RequiredCapabilities);
        Assert.Contains("unsupported-until-h3-validator", scenario.Validation.Checks);
        Assert.Contains("h3.stream.maxActiveStreams", scenario.RequiredMetrics);
    }

    [Fact]
    public void Parses_quic_transport_scenario_without_endpoint_contract()
    {
        var scenario = YamlFile.Load<ScenarioDefinition>(
            Path.Combine(TestPaths.RepoRoot, "scenarios", "quic", "transport", "duplex-streams.yaml"));

        Assert.Equal("quic.transport.duplex-streams", scenario.Id);
        Assert.Equal("quic.transport", scenario.Family);
        Assert.Equal("quic", scenario.Protocol);
        Assert.Null(scenario.Endpoint);
        Assert.Null(scenario.H3Protocol);
        Assert.NotNull(scenario.QuicTransport);
        Assert.Equal("duplex-streams", scenario.QuicTransport.Behavior);
        Assert.Equal("bidirectional", scenario.QuicTransport.StreamType);
        Assert.Equal(16, scenario.QuicTransport.StreamCount);
        Assert.Equal(2_097_152, scenario.QuicTransport.ExpectedBytes);
        Assert.Contains("quicDuplex", scenario.RequiredCapabilities);
        Assert.DoesNotContain("unsupported-until-quic-validator", scenario.Validation.Checks);
        Assert.Contains("raw-quic-load-tool-metrics", scenario.Validation.Checks);
        Assert.Contains("quic.stream.bytesReceived", scenario.RequiredMetrics);
    }

    [Fact]
    public void Parses_future_workload_family_scenarios_without_endpoint_contracts()
    {
        var webTransport = YamlFile.Load<ScenarioDefinition>(
            Path.Combine(TestPaths.RepoRoot, "scenarios", "webtransport", "session-bidi-echo.yaml"));
        var masque = YamlFile.Load<ScenarioDefinition>(
            Path.Combine(TestPaths.RepoRoot, "scenarios", "masque", "connect-udp-tunnel.yaml"));

        Assert.Equal("webtransport", webTransport.Family);
        Assert.Null(webTransport.Endpoint);
        Assert.NotNull(webTransport.WebTransport);
        Assert.Equal("session-bidi-echo", webTransport.WebTransport.Behavior);
        Assert.Contains("unsupported-until-webtransport-validator", webTransport.Validation.Checks);

        Assert.Equal("masque", masque.Family);
        Assert.Null(masque.Endpoint);
        Assert.NotNull(masque.Masque);
        Assert.Equal("connect-udp", masque.Masque.TunnelMode);
        Assert.Contains("unsupported-until-masque-validator", masque.Validation.Checks);
    }
}
