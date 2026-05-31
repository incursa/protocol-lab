// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Tests;

public sealed class AcceptanceScriptTests
{
    [Fact]
    public void Acceptance_script_models_caddy_as_optional_docker_target()
    {
        var script = File.ReadAllText(Path.Combine(
            TestPaths.RepoRoot,
            "scripts",
            "acceptance",
            "Invoke-ProtocolLabAcceptance.ps1"));

        Assert.Contains("[switch]$IncludeCaddy", script);
        Assert.Contains("$CaddyTargetImage = \"incursa/protocol-lab-caddy-bench-server:local\"", script);
        Assert.Contains("Build-CaddyBenchServerImage.ps1", script);
        Assert.Contains("$implementationSet = \"$implementationSet,caddy-http3\"", script);
        Assert.Contains("Caddy H3 validation", script);
    }

    [Fact]
    public void Acceptance_script_models_nginx_as_optional_docker_target()
    {
        var script = File.ReadAllText(Path.Combine(
            TestPaths.RepoRoot,
            "scripts",
            "acceptance",
            "Invoke-ProtocolLabAcceptance.ps1"));

        Assert.Contains("[switch]$IncludeNginx", script);
        Assert.Contains("$NginxTargetImage = \"incursa/protocol-lab-nginx-bench-server:local\"", script);
        Assert.Contains("Build-NginxBenchServerImage.ps1", script);
        Assert.Contains("$implementationSet = \"$implementationSet,nginx-http3\"", script);
        Assert.Contains("$managedImplementationSet", script);
        Assert.Contains("nginx managed-lab H3 comparison", script);
        Assert.Contains("nginx Phase 3H acceptance uses Docker h2load", script);
        Assert.Contains("nginx H3 validation", script);
        Assert.Contains("nginx is Docker-only in Phase 3H", script);
    }
}
