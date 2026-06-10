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

    [Fact]
    public void Parses_quic_go_http3_docker_target_manifest()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "quic-go-http3.yaml"));

        Assert.Equal("quic-go-http3", manifest.Id);
        Assert.Equal("quic-go HTTP/3", manifest.Name);
        Assert.Equal("docker", manifest.TargetKind);
        Assert.Equal("src/Incursa.ProtocolLab.Adapters.QuicGo/Dockerfile", manifest.Dockerfile);
        Assert.Equal("src/Incursa.ProtocolLab.Adapters.QuicGo", manifest.BuildContext);
        Assert.Equal("https://127.0.0.1:5447", manifest.DockerBaseUrl);
        Assert.Equal("https://127.0.0.1:5447", manifest.BaseUrl);
        Assert.Equal("quic-go-self-signed-loopback-certificate", manifest.CertificateMode);
        Assert.Contains("server", manifest.Roles);
        Assert.Contains("h3", manifest.SupportedProtocols);
        Assert.Contains("http.application", manifest.SupportedWorkloadFamilies);
        Assert.Contains("httpPlaintext", manifest.Capabilities);
        Assert.Contains("httpJson", manifest.Capabilities);
        Assert.Contains("httpHeaders", manifest.Capabilities);
        Assert.Contains("httpBytes", manifest.Capabilities);
        Assert.Contains("httpUpload", manifest.Capabilities);
        Assert.Equal("tcp+udp", manifest.Ports.Single().Protocol);
        Assert.Equal(8443, manifest.Ports.Single().ContainerPort);
        Assert.Equal(5447, manifest.Ports.Single().HostPort);
        Assert.Equal("http", manifest.ReadinessCheck.Type);
        Assert.Equal("/plaintext", manifest.ReadinessCheck.Url);
        Assert.Equal("docker-stop", manifest.ShutdownBehavior);
        Assert.Contains("server.stdout.txt", manifest.ArtifactExports);
        Assert.Contains("server.stderr.txt", manifest.ArtifactExports);
        Assert.False(manifest.QlogSupport);
        Assert.False(manifest.SslKeyLogSupport);
        Assert.Contains("quic-go", manifest.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expanded local comparison suite", manifest.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loads_all_reserved_implementation_manifests_from_catalog()
    {
        var manifests = ManifestCatalog.Load(Path.Combine(TestPaths.RepoRoot, "implementations"));

        Assert.Equal(4, manifests.Count);
        Assert.Contains(manifests, manifest => manifest.Id == "kestrel-http3");
        Assert.Contains(manifests, manifest => manifest.Id == "nginx-http3");
        Assert.Contains(manifests, manifest => manifest.Id == "caddy-http3");
        Assert.Contains(manifests, manifest => manifest.Id == "quic-go-http3");
    }
}
