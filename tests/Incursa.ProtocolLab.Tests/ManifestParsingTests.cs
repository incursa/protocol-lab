// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class ManifestParsingTests
{
    [Fact]
    public void Parses_kestrel_manifest()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "kestrel-http3.yaml"));

        Assert.Equal("kestrel-http3", manifest.Id);
        Assert.Contains("server", manifest.Roles);
        Assert.Contains("h3", manifest.SupportedProtocols);
        Assert.Contains("http.application", manifest.SupportedWorkloadFamilies);
        Assert.Contains("httpPlaintext", manifest.Capabilities);
        Assert.Contains("httpJson", manifest.Capabilities);
        Assert.Equal("process", manifest.TargetKind);
        Assert.Equal("incursa/protocol-lab-kestrel-bench-server:local", manifest.Image);
        Assert.Equal("dotnet", manifest.Executable);
        Assert.Equal("servers/KestrelBenchServer/KestrelBenchServer.csproj", manifest.Project);
        Assert.Equal("servers/KestrelBenchServer/Dockerfile", manifest.Dockerfile);
        Assert.Equal(".", manifest.BuildContext);
        Assert.Equal("published-port", manifest.DockerNetworkMode);
        Assert.Equal("http://127.0.0.1:5080", manifest.BaseUrl);
        Assert.Equal("http://127.0.0.1:5080", manifest.ProtocolBaseUrls["h1"]);
        Assert.Equal("https://127.0.0.1:5443", manifest.ProtocolBaseUrls["h3"]);
        Assert.Equal("https://127.0.0.1:5443", manifest.DockerProtocolBaseUrls["h3"]);
        Assert.Equal("https://0.0.0.0:5443", manifest.DockerEnvironment["PROTOCOL_LAB_H3_URL"]);
        Assert.Equal("true", manifest.DockerEnvironment["PROTOCOL_LAB_GENERATE_LOCAL_CERT"]);
        Assert.Equal("aspnetcore-development-certificate-or-explicit-pfx", manifest.CertificateMode);
        Assert.Equal("http", manifest.ReadinessCheck.Type);
        Assert.Equal("/plaintext", manifest.ReadinessCheck.Url);
        Assert.False(manifest.QlogSupport);
    }

    [Theory]
    [InlineData("kestrel-adapter-v1.yaml", null, null)]
    [InlineData("fixture-kestrel-adapter-start-failure.yaml", "servers/KestrelBenchServer/DefinitelyMissing.csproj", null)]
    [InlineData("fixture-kestrel-adapter-readiness-failure.yaml", null, "/never-ready")]
    public void Parses_kestrel_adapter_manifests(
        string fileName,
        string? benchmarkServerProjectPath,
        string? readinessProbePath)
    {
        var root = fileName.StartsWith("fixture-", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(TestPaths.RepoRoot, "tests", "Incursa.ProtocolLab.Tests", "Fixtures", "RunnerContractLab", "implementations")
            : Path.Combine(TestPaths.RepoRoot, "implementations");

        var manifest = YamlFile.Load<ImplementationManifest>(Path.Combine(root, fileName));

        Assert.Equal("adapter-v1", manifest.TargetContract);
        Assert.Equal("process", manifest.TargetKind);
        Assert.Equal("http://127.0.0.1:53171", manifest.BaseUrl);
        Assert.Equal("http://127.0.0.1:53171", manifest.AdapterControlPlaneBaseUrl);
        Assert.Contains("server", manifest.Roles);
        Assert.Contains("h1", manifest.SupportedProtocols);
        Assert.Contains("h2", manifest.SupportedProtocols);
        Assert.Contains("h3", manifest.SupportedProtocols);
        Assert.Contains("fixture.kestrel", manifest.SupportedWorkloadFamilies);
        Assert.Equal("http://127.0.0.1:53171", manifest.Environment["ASPNETCORE_URLS"]);
        Assert.Equal("/protocol-lab/adapter/v1/health", manifest.ReadinessCheck.Url);
        Assert.Contains("httpPlaintext", manifest.Capabilities);
        Assert.Contains("httpHeaders", manifest.Capabilities);

        if (benchmarkServerProjectPath is not null)
        {
            Assert.Equal(benchmarkServerProjectPath, manifest.Environment["PROTOCOL_LAB_KESTREL_BENCHMARK_PROJECT_PATH"]);
        }

        if (readinessProbePath is not null)
        {
            Assert.Equal(readinessProbePath, manifest.Environment["PROTOCOL_LAB_KESTREL_READINESS_PROBE_PATH"]);
        }
    }

    [Fact]
    public void Parses_incursa_runnable_manifest()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "incursa-http3.yaml"));

        Assert.Equal("incursa-http3", manifest.Id);
        Assert.Equal("Incursa HTTP/3", manifest.Name);
        Assert.Equal("process", manifest.TargetKind);
        Assert.Equal("incursa/protocol-lab-incursa-http3-bench-server:local", manifest.Image);
        Assert.Equal("dotnet", manifest.Executable);
        Assert.Equal("src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj", manifest.Project);
        Assert.Equal("src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Dockerfile", manifest.Dockerfile);
        Assert.Equal(".", manifest.BuildContext);
        Assert.Equal("https://localhost:5444", manifest.BaseUrl);
        Assert.Equal("https://localhost:5444", manifest.ProtocolBaseUrls["h3"]);
        Assert.Equal("https://127.0.0.1:5444", manifest.DockerBaseUrl);
        Assert.Equal("https://127.0.0.1:5444", manifest.DockerProtocolBaseUrls["h3"]);
        Assert.Equal("loopback-self-signed-certificate", manifest.CertificateMode);
        Assert.Contains("server", manifest.Roles);
        Assert.Contains("h3", manifest.SupportedProtocols);
        Assert.Contains("http.application", manifest.SupportedWorkloadFamilies);
        Assert.Contains("httpPlaintext", manifest.Capabilities);
        Assert.Contains("httpJson", manifest.Capabilities);
        Assert.Contains("httpStatus", manifest.Capabilities);
        Assert.Contains("httpBytes", manifest.Capabilities);
        Assert.Contains("httpStreaming", manifest.Capabilities);
        Assert.Contains("httpUpload", manifest.Capabilities);
        Assert.Contains("httpHeaders", manifest.Capabilities);
        Assert.Contains("--mode", manifest.CommandArguments);
        Assert.Contains("endpoint", manifest.CommandArguments);
        Assert.Contains("--port", manifest.CommandArguments);
        Assert.Contains("5444", manifest.CommandArguments);
        Assert.Equal("endpoint", manifest.Environment["PROTOCOL_LAB_INCURSA_MODE"]);
        Assert.Equal("5444", manifest.Environment["PROTOCOL_LAB_H3_PORT"]);
        Assert.Equal("endpoint", manifest.DockerEnvironment["PROTOCOL_LAB_INCURSA_MODE"]);
        Assert.Equal("5444", manifest.DockerEnvironment["PROTOCOL_LAB_H3_PORT"]);
        Assert.Contains("--mode", manifest.DockerCommandArguments);
        Assert.Contains("endpoint", manifest.DockerCommandArguments);
        Assert.Contains("--port", manifest.DockerCommandArguments);
        Assert.Contains("5444", manifest.DockerCommandArguments);
        Assert.Equal("http", manifest.ReadinessCheck.Type);
        Assert.Equal("/plaintext", manifest.ReadinessCheck.Url);
        Assert.Equal(30, manifest.ReadinessCheck.TimeoutSeconds);
        Assert.DoesNotContain("protocol-metrics/", manifest.ArtifactExports);
        Assert.False(manifest.QlogSupport);
        Assert.False(manifest.SslKeyLogSupport);
    }

    [Fact]
    public void Parses_incursa_adapter_manifest()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "incursa-http3-adapter-v1.yaml"));

        Assert.Equal("incursa-http3-adapter-v1", manifest.Id);
        Assert.Equal("Incursa HTTP/3 Adapter v1", manifest.Name);
        Assert.Equal("adapter-v1", manifest.TargetContract);
        Assert.Equal("process", manifest.TargetKind);
        Assert.Equal("src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj", manifest.Project);
        Assert.Equal("http://127.0.0.1:53172", manifest.BaseUrl);
        Assert.Equal("http://127.0.0.1:53172", manifest.AdapterControlPlaneBaseUrl);
        Assert.Contains("server", manifest.Roles);
        Assert.Contains("h3", manifest.SupportedProtocols);
        Assert.Contains("http.application", manifest.SupportedWorkloadFamilies);
        Assert.Contains("fixture.incursa-http3", manifest.SupportedWorkloadFamilies);
        Assert.Contains("adapter-control-plane", manifest.Capabilities);
        Assert.Contains("http3.server", manifest.Capabilities);
        Assert.Contains("quic.server", manifest.Capabilities);
        Assert.Contains("httpPlaintext", manifest.Capabilities);
        Assert.Contains("httpJson", manifest.Capabilities);
        Assert.Contains("httpStatus", manifest.Capabilities);
        Assert.Contains("httpBytes", manifest.Capabilities);
        Assert.Contains("httpStreaming", manifest.Capabilities);
        Assert.Contains("httpUpload", manifest.Capabilities);
        Assert.Contains("httpHeaders", manifest.Capabilities);
        Assert.Equal("http", manifest.ReadinessCheck.Type);
        Assert.Equal("/protocol-lab/adapter/v1/health", manifest.ReadinessCheck.Url);
        Assert.Equal("none", manifest.CertificateMode);
        Assert.False(manifest.QlogSupport);
        Assert.False(manifest.SslKeyLogSupport);
    }

    [Fact]
    public void Parses_caddy_runnable_docker_manifest()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "caddy-http3.yaml"));

        Assert.Equal("caddy-http3", manifest.Id);
        Assert.Equal("Caddy HTTP/3", manifest.Name);
        Assert.Equal("docker", manifest.TargetKind);
        Assert.Equal("incursa/protocol-lab-caddy-bench-server:local", manifest.Image);
        Assert.Equal("servers/caddy/Dockerfile", manifest.Dockerfile);
        Assert.Equal("servers/caddy", manifest.BuildContext);
        Assert.Equal("published-port", manifest.DockerNetworkMode);
        Assert.Equal("https://localhost:5445", manifest.DockerBaseUrl);
        Assert.Equal("https://localhost:5445", manifest.DockerProtocolBaseUrls["h3"]);
        Assert.Equal("caddy-internal-local-ca-loopback-certificate", manifest.CertificateMode);
        Assert.Contains("server", manifest.Roles);
        Assert.Contains("h3", manifest.SupportedProtocols);
        Assert.Contains("http.application", manifest.SupportedWorkloadFamilies);
        Assert.Contains("httpPlaintext", manifest.Capabilities);
        Assert.Contains("httpJson", manifest.Capabilities);
        Assert.Equal("https-h3", manifest.Ports.Single().Name);
        Assert.Equal(8443, manifest.Ports.Single().ContainerPort);
        Assert.Equal(5445, manifest.Ports.Single().HostPort);
        Assert.Equal("tcp+udp", manifest.Ports.Single().Protocol);
        Assert.Contains("caddy", manifest.DockerCommandArguments);
        Assert.Contains("run", manifest.DockerCommandArguments);
        Assert.Contains("/etc/caddy/Caddyfile", manifest.DockerCommandArguments);
        Assert.Equal("http", manifest.ReadinessCheck.Type);
        Assert.Equal("/plaintext", manifest.ReadinessCheck.Url);
        Assert.True(manifest.ReadinessCheck.StartupDelayMilliseconds > 0);
        Assert.False(manifest.QlogSupport);
        Assert.False(manifest.SslKeyLogSupport);
        Assert.Contains("not publishable", manifest.Notes);
    }

    [Fact]
    public void Parses_nginx_runnable_docker_manifest()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "nginx-http3.yaml"));

        Assert.Equal("nginx-http3", manifest.Id);
        Assert.Equal("nginx HTTP/3", manifest.Name);
        Assert.Equal("docker", manifest.TargetKind);
        Assert.Equal("incursa/protocol-lab-nginx-bench-server:local", manifest.Image);
        Assert.Equal("servers/nginx/Dockerfile", manifest.Dockerfile);
        Assert.Equal("servers/nginx", manifest.BuildContext);
        Assert.Equal("published-port", manifest.DockerNetworkMode);
        Assert.Equal("https://localhost:5446", manifest.DockerBaseUrl);
        Assert.Equal("https://localhost:5446", manifest.DockerProtocolBaseUrls["h3"]);
        Assert.Equal("nginx-self-signed-localhost-loopback-certificate", manifest.CertificateMode);
        Assert.Contains("server", manifest.Roles);
        Assert.Contains("h3", manifest.SupportedProtocols);
        Assert.Contains("http.application", manifest.SupportedWorkloadFamilies);
        Assert.Contains("httpPlaintext", manifest.Capabilities);
        Assert.Contains("httpJson", manifest.Capabilities);
        Assert.Equal("https-h3", manifest.Ports.Single().Name);
        Assert.Equal(8446, manifest.Ports.Single().ContainerPort);
        Assert.Equal(5446, manifest.Ports.Single().HostPort);
        Assert.Equal("tcp+udp", manifest.Ports.Single().Protocol);
        Assert.NotNull(manifest.TargetCapabilityProof);
        Assert.Equal("nginx-http3-module", manifest.TargetCapabilityProof!.Id);
        Assert.True(manifest.TargetCapabilityProof.Required);
        Assert.Contains("nginx", manifest.TargetCapabilityProof.DockerExecArguments);
        Assert.Equal("--with-http_v3_module", manifest.TargetCapabilityProof.ExpectedOutputContains);
        Assert.Contains("nginx", manifest.DockerCommandArguments);
        Assert.Contains("daemon off;", manifest.DockerCommandArguments);
        Assert.Equal("http", manifest.ReadinessCheck.Type);
        Assert.Equal("/plaintext", manifest.ReadinessCheck.Url);
        Assert.True(manifest.ReadinessCheck.StartupDelayMilliseconds > 0);
        Assert.False(manifest.QlogSupport);
        Assert.False(manifest.SslKeyLogSupport);
        Assert.Contains("not publishable", manifest.Notes);
    }

    [Theory]
    [InlineData("quic-go-http3.yaml", "quic-go-http3")]
    public void Parses_deferred_placeholder_manifests_without_claiming_support(
        string fileName,
        string implementationId)
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", fileName));

        Assert.Equal(implementationId, manifest.Id);
        Assert.Contains("server", manifest.Roles);
        Assert.Empty(manifest.SupportedProtocols);
        Assert.Empty(manifest.SupportedWorkloadFamilies);
        Assert.Empty(manifest.Capabilities);
        Assert.Contains("server.stdout.txt", manifest.ArtifactExports);
        Assert.Contains("server.stderr.txt", manifest.ArtifactExports);
        Assert.False(manifest.QlogSupport);
        Assert.False(manifest.SslKeyLogSupport);
        Assert.Contains("Placeholder only", manifest.Notes);
    }

    [Fact]
    public void Loads_all_reserved_implementation_manifests_from_catalog()
    {
        var manifests = ManifestCatalog.Load(Path.Combine(TestPaths.RepoRoot, "implementations"));

        Assert.Equal(9, manifests.Count);
        Assert.Contains(manifests, manifest => manifest.Id == "kestrel-http3");
        Assert.Contains(manifests, manifest => manifest.Id == "incursa-http3");
        Assert.Contains(manifests, manifest => manifest.Id == "nginx-http3");
        Assert.Contains(manifests, manifest => manifest.Id == "caddy-http3");
        Assert.Contains(manifests, manifest => manifest.Id == "quic-go-http3");
        Assert.Contains(manifests, manifest => manifest.Id == "kestrel-adapter-v1");
        Assert.Contains(manifests, manifest => manifest.Id == "incursa-http3-adapter-v1");
        Assert.Contains(manifests, manifest => manifest.Id == "msquic-dotnet-raw-adapter-v1");
        Assert.Contains(manifests, manifest => manifest.Id == "incursa-raw-quic-adapter-v1");
    }
}
