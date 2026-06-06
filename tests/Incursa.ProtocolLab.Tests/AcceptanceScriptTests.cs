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

    [Fact]
    public void Local_public_report_script_runs_benchmark_and_stages_bundle_without_cloud_upload()
    {
        var script = File.ReadAllText(Path.Combine(
            TestPaths.RepoRoot,
            "scripts",
            "publication",
            "New-ProtocolLabPublicReportBundle.ps1"));

        Assert.Contains("This script does not upload to R2.", script);
        Assert.Contains("\"run\",", script);
        Assert.Contains("\"publish-report\",", script);
        Assert.Contains("--publication-output", script);
        Assert.Contains("--skip-publication-bundle", script);
        Assert.Contains("--allow-diagnostic-publication", script);
        Assert.Contains("evidence-report-v1.json", script);
        Assert.Contains("artifacts-index.json", script);
        Assert.Contains("report-index.json", script);
    }

    [Fact]
    public void R2_upload_script_exposes_bundle_root_dry_run_and_object_verification()
    {
        var script = File.ReadAllText(Path.Combine(
            TestPaths.RepoRoot,
            "scripts",
            "publication",
            "Upload-ProtocolLabReportBundle.ps1"));

        Assert.Contains("[string]$BundleRoot", script);
        Assert.Contains("[string]$RunRoot", script);
        Assert.Contains("[int]$UploadConcurrency = 8", script);
        Assert.Contains("[string]$R2CredentialsPath", script);
        Assert.Contains("PROTOCOL_LAB_R2_CREDENTIALS_PATH", script);
        Assert.Contains("PowerShell SecretManagement", script);
        Assert.Contains("[switch]$VerifyUploadedObjects", script);
        Assert.Contains("[switch]$DryRun", script);
        Assert.Contains("public/runs/$RunId/", script);
        Assert.DoesNotContain("C:\\temp", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("public/registry/", script, StringComparison.OrdinalIgnoreCase);
    }
}
