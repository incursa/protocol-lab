// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Runner;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Tests;

public sealed class TargetLifecycleTests
{
    [Fact]
    public void Defines_neutral_target_lifecycle_vocabulary()
    {
        Assert.Equal("process", TargetKinds.Process);
        Assert.Equal("docker", TargetKinds.Docker);
        Assert.Equal("external", TargetKinds.External);
        Assert.Equal("published-port", TargetNetworkModes.PublishedPort);
        Assert.Equal("shared-docker-network", TargetNetworkModes.SharedDockerNetwork);

        Assert.Equal("start", TargetLifecycleSteps.Start);
        Assert.Equal("waitReady", TargetLifecycleSteps.WaitReady);
        Assert.Equal("collectArtifacts", TargetLifecycleSteps.CollectArtifacts);
        Assert.Equal("stop", TargetLifecycleSteps.Stop);

        Assert.Equal("http", ReadinessCheckTypes.Http);
        Assert.Equal("tcp", ReadinessCheckTypes.Tcp);
        Assert.Equal("processStarted", ReadinessCheckTypes.ProcessStarted);
        Assert.Equal("none", ReadinessCheckTypes.None);
    }

    [Fact]
    public void Readiness_check_models_http_readiness_for_udp_h3_targets()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "incursa-http3.yaml"));

        Assert.Equal(ReadinessCheckTypes.Http, manifest.ReadinessCheck.Type);
        Assert.Equal("/plaintext", manifest.ReadinessCheck.Url);
        Assert.Equal(30, manifest.ReadinessCheck.TimeoutSeconds);
        Assert.NotEqual(ReadinessCheckTypes.Tcp, manifest.ReadinessCheck.Type);
    }

    [Fact]
    public void Serializes_target_execution_result()
    {
        var result = new TargetExecutionResult
        {
            Status = TargetExecutionStatuses.Ready,
            Started = true,
            Ready = true,
            StartTimeUtc = DateTimeOffset.UnixEpoch,
            ReadyTimeUtc = DateTimeOffset.UnixEpoch.AddSeconds(1),
            StdoutPath = "target.stdout.txt",
            StderrPath = "target.stderr.txt",
            LogsPath = "logs"
        };

        var json = ResultJson.Serialize(result);
        var roundTrip = ResultJson.Deserialize<TargetExecutionResult>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(TargetExecutionStatuses.Ready, roundTrip.Status);
        Assert.True(roundTrip.Started);
        Assert.True(roundTrip.Ready);
        Assert.Equal("target.stdout.txt", roundTrip.StdoutPath);
    }

    [Fact]
    public void Builds_docker_target_command_with_published_tcp_and_udp_ports()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "kestrel-http3.yaml"));

        var commandLine = TargetOrchestrator.BuildDockerRunCommandLineForTest(
            manifest,
            manifest.Image,
            "protocol-lab-test",
            TargetNetworkModes.PublishedPort,
            dockerNetwork: null);

        Assert.Contains("docker run", commandLine);
        Assert.Contains("--name protocol-lab-test", commandLine);
        Assert.Contains("--publish 5080:5080/tcp", commandLine);
        Assert.Contains("--publish 5443:5443/tcp", commandLine);
        Assert.Contains("--publish 5443:5443/udp", commandLine);
        Assert.Contains("PROTOCOL_LAB_H3_URL=https://0.0.0.0:5443", commandLine);
        Assert.Contains(manifest.Image, commandLine);
    }

    [Fact]
    public void Builds_incursa_docker_target_command_with_published_udp_port()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "incursa-http3.yaml"));

        var commandLine = TargetOrchestrator.BuildDockerRunCommandLineForTest(
            manifest,
            manifest.Image,
            "protocol-lab-incursa-test",
            TargetNetworkModes.PublishedPort,
            dockerNetwork: null);

        Assert.Contains("docker run", commandLine);
        Assert.Contains("--name protocol-lab-incursa-test", commandLine);
        Assert.Contains("--publish 5444:5444/udp", commandLine);
        Assert.DoesNotContain("--publish 5444:5444/tcp", commandLine);
        Assert.Contains("PROTOCOL_LAB_INCURSA_MODE=endpoint", commandLine);
        Assert.Contains("PROTOCOL_LAB_H3_PORT=5444", commandLine);
        Assert.Contains("--mode endpoint", commandLine);
        Assert.Contains("--port 5444", commandLine);
        Assert.Contains(manifest.Image, commandLine);
    }

    [Fact]
    public void Builds_caddy_docker_target_command_with_published_tcp_and_udp_ports()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "caddy-http3.yaml"));

        var commandLine = TargetOrchestrator.BuildDockerRunCommandLineForTest(
            manifest,
            manifest.Image,
            "protocol-lab-caddy-test",
            TargetNetworkModes.PublishedPort,
            dockerNetwork: null);

        Assert.Contains("docker run", commandLine);
        Assert.Contains("--name protocol-lab-caddy-test", commandLine);
        Assert.Contains("--publish 5445:8443/tcp", commandLine);
        Assert.Contains("--publish 5445:8443/udp", commandLine);
        Assert.Contains("PROTOCOL_LAB_IMPLEMENTATION=caddy-http3", commandLine);
        Assert.Contains("caddy run --config /etc/caddy/Caddyfile --adapter caddyfile", commandLine);
        Assert.Contains(manifest.Image, commandLine);
    }

    [Fact]
    public void Builds_nginx_docker_target_command_with_published_tcp_and_udp_ports_and_capability_proof_metadata()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "nginx-http3.yaml"));

        var commandLine = TargetOrchestrator.BuildDockerRunCommandLineForTest(
            manifest,
            manifest.Image,
            "protocol-lab-nginx-test",
            TargetNetworkModes.PublishedPort,
            dockerNetwork: null);

        Assert.Contains("docker run", commandLine);
        Assert.Contains("--name protocol-lab-nginx-test", commandLine);
        Assert.Contains("--publish 5446:8446/tcp", commandLine);
        Assert.Contains("--publish 5446:8446/udp", commandLine);
        Assert.Contains("PROTOCOL_LAB_IMPLEMENTATION=nginx-http3", commandLine);
        Assert.Contains("nginx -g \"daemon off;\"", commandLine);
        Assert.Contains(manifest.Image, commandLine);
        Assert.NotNull(manifest.TargetCapabilityProof);
        Assert.Equal("--with-http_v3_module", manifest.TargetCapabilityProof!.ExpectedOutputContains);
    }

    [Fact]
    public void Builds_docker_target_command_with_shared_network_alias_and_validation_ports()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "kestrel-http3.yaml"));

        var commandLine = TargetOrchestrator.BuildDockerRunCommandLineForTest(
            manifest,
            manifest.Image,
            "protocol-lab-kestrel-test",
            TargetNetworkModes.SharedDockerNetwork,
            dockerNetwork: "protocol-lab-run-1");

        Assert.Contains("--network protocol-lab-run-1", commandLine);
        Assert.Contains("--network-alias kestrel-http3", commandLine);
        Assert.Contains("--publish 5443:5443/udp", commandLine);
        Assert.Contains("--publish 5443:5443/tcp", commandLine);
    }

    [Fact]
    public void Builds_caddy_docker_target_command_with_shared_network_alias_resource_limits_and_validation_ports()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "caddy-http3.yaml"));

        var commandLine = TargetOrchestrator.BuildDockerRunCommandLineForTest(
            manifest,
            manifest.Image,
            "protocol-lab-caddy-test",
            TargetNetworkModes.SharedDockerNetwork,
            dockerNetwork: "protocol-lab-run-1",
            resourceLimits: new DockerResourceLimits { Cpus = "2", Memory = "1g" });

        Assert.Contains("--network protocol-lab-run-1", commandLine);
        Assert.Contains("--network-alias caddy-http3", commandLine);
        Assert.Contains("--publish 5445:8443/udp", commandLine);
        Assert.Contains("--publish 5445:8443/tcp", commandLine);
        Assert.Contains("--cpus 2", commandLine);
        Assert.Contains("--memory 1g", commandLine);
    }

    [Fact]
    public void Builds_nginx_docker_target_command_with_shared_network_alias_resource_limits_and_validation_ports()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "nginx-http3.yaml"));

        var commandLine = TargetOrchestrator.BuildDockerRunCommandLineForTest(
            manifest,
            manifest.Image,
            "protocol-lab-nginx-test",
            TargetNetworkModes.SharedDockerNetwork,
            dockerNetwork: "protocol-lab-run-1",
            resourceLimits: new DockerResourceLimits { Cpus = "2", Memory = "1g" });

        Assert.Contains("--network protocol-lab-run-1", commandLine);
        Assert.Contains("--network-alias nginx-http3", commandLine);
        Assert.Contains("--publish 5446:8446/udp", commandLine);
        Assert.Contains("--publish 5446:8446/tcp", commandLine);
        Assert.Contains("--cpus 2", commandLine);
        Assert.Contains("--memory 1g", commandLine);
    }

    [Fact]
    public void Builds_docker_target_command_with_resource_limits()
    {
        var manifest = YamlFile.Load<ImplementationManifest>(
            Path.Combine(TestPaths.RepoRoot, "implementations", "kestrel-http3.yaml"));

        var commandLine = TargetOrchestrator.BuildDockerRunCommandLineForTest(
            manifest,
            manifest.Image,
            "protocol-lab-kestrel-test",
            TargetNetworkModes.SharedDockerNetwork,
            dockerNetwork: "protocol-lab-run-1",
            resourceLimits: new DockerResourceLimits
            {
                Cpus = "2",
                Memory = "1g",
                MemorySwap = "1g",
                CpusetCpus = "0-1",
                PidsLimit = 512
            });

        Assert.Contains("--cpus 2", commandLine);
        Assert.Contains("--memory 1g", commandLine);
        Assert.Contains("--memory-swap 1g", commandLine);
        Assert.Contains("--cpuset-cpus 0-1", commandLine);
        Assert.Contains("--pids-limit 512", commandLine);
    }

    [Fact]
    public void Generates_stable_docker_network_names_and_aliases()
    {
        Assert.Equal("protocol-lab-local-phase3c-run", TargetOrchestrator.GenerateDockerNetworkName("local phase3c/run"));
        Assert.Equal("incursa-http3", TargetOrchestrator.GenerateNetworkAlias("Incursa_HTTP3"));
    }

    [Fact]
    public async Task Docker_target_mode_without_local_image_returns_unsupported_result()
    {
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-target-{Guid.NewGuid():N}");
        var cell = new RunCell(
            new ImplementationManifest
            {
                Id = "docker-placeholder",
                Name = "Docker placeholder",
                TargetKind = TargetKinds.Docker,
                Image = "example/missing:local",
                BaseUrl = "https://127.0.0.1:5443"
            },
            new ScenarioDefinition { Id = "http.core.plaintext", Protocol = "h3" },
            "h3",
            1,
            1,
            1,
            30,
            5,
            "clean");

        try
        {
            var paths = ArtifactLayout.GetCellPaths(output, "run-1", cell);
            Directory.CreateDirectory(paths.CellDirectory);

            await using var target = await TargetOrchestrator.StartAsync(
                TestPaths.RepoRoot,
                cell.Implementation,
                externalBaseUrl: null,
                paths,
                requestedProtocol: "h3",
                new TargetStartOptions(Mode: TargetKinds.Docker, DockerImageOverride: "example/missing:local"));

            Assert.True(target.Result.Unsupported);
            Assert.Equal(TargetKinds.Docker, target.Result.TargetExecutionMode);
            Assert.Contains(target.Result.Errors, error =>
                error.Contains("Docker executable was not found", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("was not found locally", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact]
    public async Task External_target_mode_is_ready_without_starting_process()
    {
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-target-{Guid.NewGuid():N}");
        var cell = new RunCell(
            new ImplementationManifest { Id = "external", Name = "External" },
            new ScenarioDefinition { Id = "http.core.plaintext", Protocol = "h1" },
            "h1",
            1,
            1,
            1,
            30,
            5,
            "clean");

        try
        {
            var paths = ArtifactLayout.GetCellPaths(output, "run-1", cell);
            Directory.CreateDirectory(paths.CellDirectory);

            await using var target = await TargetOrchestrator.StartAsync(
                TestPaths.RepoRoot,
                cell.Implementation,
                "http://127.0.0.1:5000",
                paths);

            Assert.Equal("http://127.0.0.1:5000", target.BaseUrl);
            Assert.True(target.Result.Ready);
            Assert.False(target.Result.Started);
            Assert.True(File.Exists(paths.TargetExecutionJson));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Missing_startup_manifest_returns_unsupported_target()
    {
        var output = Path.Combine(Path.GetTempPath(), $"protocol-lab-target-{Guid.NewGuid():N}");
        var cell = new RunCell(
            new ImplementationManifest { Id = "placeholder", Name = "Placeholder" },
            new ScenarioDefinition { Id = "http.core.plaintext", Protocol = "h1" },
            "h1",
            1,
            1,
            1,
            30,
            5,
            "clean");

        try
        {
            var paths = ArtifactLayout.GetCellPaths(output, "run-1", cell);
            Directory.CreateDirectory(paths.CellDirectory);

            await using var target = await TargetOrchestrator.StartAsync(
                TestPaths.RepoRoot,
                cell.Implementation,
                externalBaseUrl: null,
                paths);

            Assert.True(target.Result.Unsupported);
            Assert.Contains("does not define target startup", target.Result.Errors[0]);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }
}
