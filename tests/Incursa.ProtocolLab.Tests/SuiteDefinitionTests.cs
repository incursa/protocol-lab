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
        Assert.Contains("incursa-http3", suite.Implementations);
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
    public void Parses_h3_local_v1_docker_target_suite()
    {
        var suite = YamlFile.Load<SuiteDefinition>(Path.Combine(TestPaths.RepoRoot, "suites", "h3-local-v1-docker-target.yaml"));

        Assert.Equal("h3-local-v1-docker-target", suite.Id);
        Assert.Equal("docker", suite.TargetMode);
        Assert.Equal("published-port", suite.TargetNetworkMode);
        Assert.Contains("kestrel-http3", suite.Implementations);
        Assert.Contains("incursa-http3", suite.Implementations);
        Assert.DoesNotContain(suite.UnsupportedImplementations, implementation => implementation.Id == "incursa-http3");
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
        Assert.Contains("incursa-http3", suite.Implementations);
        Assert.Contains(suite.LoadTools, tool => tool.Id == "h2load" && tool.Mode == "docker");
        Assert.Contains("shared Docker network", suite.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parses_kestrel_adapter_fixture_suite()
    {
        var suite = YamlFile.Load<SuiteDefinition>(
            Path.Combine(TestPaths.RepoRoot, "tests", "Incursa.ProtocolLab.Tests", "Fixtures", "RunnerContractLab", "suites", "runner-kestrel-adapter.fixture.yaml"));

        Assert.Equal("runner-kestrel-adapter-fixture", suite.Id);
        Assert.Equal("process", suite.TargetMode);
        Assert.Equal("published-port", suite.TargetNetworkMode);
        Assert.Contains("fixture-kestrel-adapter-v1", suite.Implementations);
        Assert.Contains("fixture.kestrel.success", suite.Scenarios);
        Assert.Contains("fixture.kestrel.inspect-headers", suite.Scenarios);
        Assert.Contains(suite.LoadTools, tool => tool.Id == "fixture-load-success" && tool.Mode == "process");
        Assert.Equal("h1", suite.Protocol);
        Assert.Contains("protocol endpoint", suite.Notes, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("incursa-http3", suite.Implementations);
        Assert.Contains("caddy-http3", suite.Implementations);
        Assert.Contains("nginx-http3", suite.Implementations);
        Assert.Equal("shared-docker-network", suite.TargetNetworkMode);
    }
}
