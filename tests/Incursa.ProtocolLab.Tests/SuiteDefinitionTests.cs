// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class SuiteDefinitionTests
{
    [Fact]
    public void Parses_h3_local_v1_suite()
    {
        var suite = YamlFile.Load<SuiteDefinition>(Path.Combine(TestPaths.RepoRoot, "suites", "h3-local-v1.yaml"));

        Assert.Equal("h3-local-v1", suite.Id);
        Assert.Equal("h3", suite.Protocol);
        Assert.Contains("kestrel-http3", suite.Implementations);
        Assert.Contains("http.core.plaintext", suite.Scenarios);
        Assert.Contains("http.core.json", suite.Scenarios);
        Assert.Contains(suite.LoadTools, tool => tool.Id == "managed-httpclient-h3-load" && tool.Mode == "managed");
        Assert.Contains(suite.LoadTools, tool => tool.Id == "h2load" && tool.Mode == "docker");
        Assert.Equal(5, suite.Defaults.DurationSeconds);
        Assert.Equal(1, suite.Defaults.WarmupSeconds);
        Assert.Equal(16, suite.Defaults.Connections);
        Assert.False(suite.CounterCapture.EnabledByDefault);
        Assert.Equal("dotnet-counters", suite.CounterCapture.Tool);
    }

    [Fact]
    public void Parses_h3_local_v1_comparison_suite_with_runnable_quic_go_target()
    {
        var suite = YamlFile.Load<SuiteDefinition>(Path.Combine(TestPaths.RepoRoot, "suites", "h3-local-v1-comparison.yaml"));

        Assert.Equal("h3-local-v1-comparison", suite.Id);
        Assert.Equal("local-comparison", suite.LoadProfileId);
        Assert.Contains("kestrel-http3", suite.Implementations);
        Assert.Contains("quic-go-http3", suite.Implementations);
        Assert.Empty(suite.UnsupportedImplementations);
        Assert.Equal(12, suite.Scenarios.Count);
        Assert.Contains("http.headers.inspect-request", suite.Scenarios);
        Assert.Contains("http.headers.response.50x32", suite.Scenarios);
        Assert.Contains("http.core.status", suite.Scenarios);
        Assert.Contains("http.payload.bytes.1kb", suite.Scenarios);
        Assert.Contains("http.payload.bytes.64kb", suite.Scenarios);
        Assert.Contains("http.payload.bytes.1mb", suite.Scenarios);
        Assert.Contains("http.payload.stream.100x16kb", suite.Scenarios);
        Assert.Contains("http.upload.echo.64kb", suite.Scenarios);
        Assert.Contains("http.upload.hash.1mb", suite.Scenarios);
        Assert.Contains("http.upload.sink.1mb", suite.Scenarios);
        Assert.Contains(suite.LoadTools, tool => tool.Id == "managed-httpclient-h3-load" && tool.Mode == "managed" && tool.Category == "managed-lab");
        Assert.Equal(30, suite.Defaults.DurationSeconds);
        Assert.Equal(10, suite.Defaults.WarmupSeconds);
        Assert.Equal(3, suite.Defaults.Repetitions);
        Assert.Equal(128, suite.Defaults.Connections);
        Assert.Equal(100, suite.Defaults.StreamsPerConnection);
        Assert.Contains("package v2", suite.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full stable", suite.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parses_quic_transport_v1_comparison_suite_with_raw_quic_targets()
    {
        var suite = YamlFile.Load<SuiteDefinition>(Path.Combine(TestPaths.RepoRoot, "suites", "quic-transport-v1-comparison.yaml"));

        Assert.Equal("quic-transport-v1-comparison", suite.Id);
        Assert.Equal("quic", suite.Protocol);
        Assert.Equal("local-comparison", suite.LoadProfileId);
        Assert.Equal("process", suite.TargetMode);
        Assert.Equal("published-port", suite.TargetNetworkMode);
        Assert.Empty(suite.Implementations);
        Assert.Equal(2, suite.Scenarios.Count);
        Assert.Contains("quic.transport.multiplex.100x64kb", suite.Scenarios);
        Assert.Contains("quic.transport.duplex-streams", suite.Scenarios);
        Assert.Contains(suite.LoadTools, tool => tool.Id == "quic-go-raw-load" && tool.Mode == "process" && tool.Category == "managed-lab");
        Assert.Equal(30, suite.Defaults.DurationSeconds);
        Assert.Equal(10, suite.Defaults.WarmupSeconds);
        Assert.Equal(3, suite.Defaults.Repetitions);
        Assert.Equal(32, suite.Defaults.Connections);
        Assert.Equal(16, suite.Defaults.StreamsPerConnection);
        Assert.Contains("package-backed", suite.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parses_h3_local_v1_docker_target_suite()
    {
        var suite = YamlFile.Load<SuiteDefinition>(Path.Combine(TestPaths.RepoRoot, "suites", "h3-local-v1-docker-target.yaml"));

        Assert.Equal("h3-local-v1-docker-target", suite.Id);
        Assert.Equal("docker", suite.TargetMode);
        Assert.Equal("published-port", suite.TargetNetworkMode);
        Assert.Contains("kestrel-http3", suite.Implementations);
        Assert.Contains(suite.LoadTools, tool => tool.Id == "h2load" && tool.Mode == "docker");
    }

    [Fact]
    public void Parses_h3_local_v1_docker_target_shared_network_suite()
    {
        var suite = YamlFile.Load<SuiteDefinition>(Path.Combine(TestPaths.RepoRoot, "suites", "h3-local-v1-docker-target-shared-network.yaml"));

        Assert.Equal("h3-local-v1-docker-target-shared-network", suite.Id);
        Assert.Equal("docker", suite.TargetMode);
        Assert.Equal("shared-docker-network", suite.TargetNetworkMode);
        Assert.Contains("kestrel-http3", suite.Implementations);
        Assert.Contains(suite.LoadTools, tool => tool.Id == "h2load" && tool.Mode == "docker");
        Assert.Contains("shared Docker network", suite.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parses_caddy_docker_target_suite()
    {
        var suite = YamlFile.Load<SuiteDefinition>(Path.Combine(TestPaths.RepoRoot, "suites", "h3-local-docker-target-caddy.yaml"));

        Assert.Equal("h3-local-docker-target-caddy", suite.Id);
        Assert.Equal("docker", suite.TargetMode);
        Assert.Equal("shared-docker-network", suite.TargetNetworkMode);
        Assert.Equal("h3", suite.Protocol);
        Assert.Contains("caddy-http3", suite.Implementations);
        Assert.Contains("http.core.plaintext", suite.Scenarios);
        Assert.Contains("http.core.json", suite.Scenarios);
        Assert.Contains(suite.LoadTools, tool => tool.Id == "h2load" && tool.Mode == "docker");
        Assert.Equal(5, suite.Defaults.DurationSeconds);
        Assert.Equal(1, suite.Defaults.WarmupSeconds);
        Assert.Equal(16, suite.Defaults.Connections);
        Assert.Contains("Caddy", suite.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parses_nginx_docker_target_suite()
    {
        var suite = YamlFile.Load<SuiteDefinition>(Path.Combine(TestPaths.RepoRoot, "suites", "h3-local-docker-target-nginx.yaml"));

        Assert.Equal("h3-local-docker-target-nginx", suite.Id);
        Assert.Equal("docker", suite.TargetMode);
        Assert.Equal("shared-docker-network", suite.TargetNetworkMode);
        Assert.Equal("h3", suite.Protocol);
        Assert.Contains("nginx-http3", suite.Implementations);
        Assert.Contains("http.core.plaintext", suite.Scenarios);
        Assert.Contains("http.core.json", suite.Scenarios);
        Assert.Contains(suite.LoadTools, tool => tool.Id == "h2load" && tool.Mode == "docker");
        Assert.Equal(5, suite.Defaults.DurationSeconds);
        Assert.Equal(1, suite.Defaults.WarmupSeconds);
        Assert.Equal(16, suite.Defaults.Connections);
        Assert.Contains("nginx", suite.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HTTP/3 module proof", suite.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parses_baseline_docker_target_suite_with_caddy_and_nginx_optional_baselines()
    {
        var suite = YamlFile.Load<SuiteDefinition>(Path.Combine(TestPaths.RepoRoot, "suites", "h3-local-docker-target-baselines.yaml"));

        Assert.Equal("h3-local-docker-target-baselines", suite.Id);
        Assert.Contains("kestrel-http3", suite.Implementations);
        Assert.Contains("caddy-http3", suite.Implementations);
        Assert.Contains("nginx-http3", suite.Implementations);
        Assert.Equal("shared-docker-network", suite.TargetNetworkMode);
    }
}
