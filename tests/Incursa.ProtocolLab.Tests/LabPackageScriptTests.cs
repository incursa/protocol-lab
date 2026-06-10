// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Tests;

public sealed class LabPackageScriptTests
{
    [Fact]
    public void Package_builder_accepts_v2_component_kinds_while_preserving_v1_implementation_only_rule()
    {
        var script = Read("scripts", "lab", "New-ProtocolLabPackage.ps1");

        Assert.Contains("protocol-lab-package-v1", script);
        Assert.Contains("protocol-lab-package-v2", script);
        Assert.Contains("\"implementation\", \"test-executor\", \"scenario-pack\", \"toolchain\"", script);
        Assert.Contains("V1 supports only 'implementation'", script);
        Assert.Contains("dependencies.requiresGo", script);
        Assert.Contains("Assert-V2ProvidedComponents", script);
        Assert.Contains("providedImplementations", script);
        Assert.Contains("providedTestExecutors", script);
        Assert.Contains("providedSuites or providedScenarios", script);
        Assert.Contains("Assert-V2EntryManifestLayout", script);
        Assert.Contains("V2 $($Manifest.kind) entryManifests must live under", script);
        Assert.Contains("must not be a URI path", script);
        Assert.Contains("kind = [string]$manifest.kind", script);
    }

    [Fact]
    public void Package_submitter_supports_multiple_component_package_references()
    {
        var script = Read("scripts", "lab", "Submit-ProtocolLabPackageRun.ps1");

        Assert.Contains("[string[]] $AdditionalPackagePath", script);
        Assert.Contains("[string[]] $PackageReference", script);
        Assert.Contains("[string] $TestExecutorId", script);
        Assert.Contains("Send-PackageArchive", script);
        Assert.Contains("PackageReference values must use packageId:packageVersion:sha256 form", script);
        Assert.Contains("packages = $packages", script);
        Assert.Contains("suiteIds = @($SuiteId)", script);
        Assert.Contains("testExecutorIds = @($TestExecutorId)", script);
        Assert.Contains("protocols = @($Protocol)", script);
        Assert.Contains("targetMode = $ExecutionMode", script);
        Assert.DoesNotContain("suiteId = $SuiteId", script);
        Assert.DoesNotContain("protocol = $Protocol", script);
        Assert.DoesNotContain("executionMode = $ExecutionMode", script);
    }

    [Fact]
    public void Raw_quic_component_package_builder_emits_test_executor_and_scenario_pack()
    {
        var script = Read("scripts", "lab", "New-ProtocolLabRawQuicComponentPackages.ps1");

        Assert.Contains("protocol-lab-raw-quic-test-executor", script);
        Assert.Contains("protocol-lab-raw-quic-scenarios", script);
        Assert.Contains("kind = \"test-executor\"", script);
        Assert.Contains("kind = \"scenario-pack\"", script);
        Assert.Contains("test-executors/quic-go-raw-load.yaml", script);
        Assert.Contains("Test Executor Contract v1 manifest", script);
        Assert.Contains("go -C $testExecutorRoot build -trimpath", script);
        Assert.Contains("bin/$RuntimeIdentifier/quic-go-raw-load", script);
        Assert.Contains("tools/test-executors/quic-go-raw-load", script);
        Assert.Contains("source/go.mod", script);
        Assert.Contains("requiresGo = -not $BinaryBacked", script);
        Assert.Contains("SourceBackedTestExecutor", script);
        Assert.Contains("quic-transport-v1-comparison.yaml", script);
        Assert.Contains("packageReferences", script);
        Assert.Contains("quic.transport.multiplex.100x64kb", script);
        Assert.Contains("quic.transport.duplex-streams", script);
        Assert.DoesNotContain("incursa-raw-quic-adapter-v1", script);
        Assert.DoesNotContain("src/Incursa.ProtocolLab.Adapters.QuicGo", script);
        Assert.DoesNotContain("quic.transport.stream-throughput.1mb", script);
        Assert.DoesNotContain("quic.transport.connection-churn", script);
        Assert.DoesNotContain("quic.transport.handshake-cold", script);
    }

    [Fact]
    public void H3_component_package_builder_emits_large_body_managed_executor_and_scenario_pack()
    {
        var script = Read("scripts", "lab", "New-ProtocolLabH3ComponentPackages.ps1");

        Assert.Contains("protocol-lab-managed-h3-test-executor", script);
        Assert.Contains("protocol-lab-h3-large-body-scenarios", script);
        Assert.Contains("kind = \"test-executor\"", script);
        Assert.Contains("kind = \"scenario-pack\"", script);
        Assert.Contains("test-executors/managed-httpclient-h3-load.yaml", script);
        Assert.Contains("Test Executor Contract v1 manifest", script);
        Assert.Contains("availabilityCheck: managed", script);
        Assert.Contains("managed-httpclient-h3-json", script);
        Assert.Contains("h3-large-body-v1", script);
        Assert.Contains("suites/$SelectedSuiteId.yaml", script);
        Assert.Contains("http.payload.bytes.64kb", script);
        Assert.Contains("http.payload.bytes.1mb", script);
        Assert.Contains("H3 component package builder only supports explicit large-body scenarios", script);
        Assert.Contains("requiresBash = $false", script);
        Assert.DoesNotContain("quic.transport.multiplex.100x64kb", script);
        Assert.DoesNotContain("quic-go-raw-load", script);
        Assert.DoesNotContain("http.upload.hash.1mb", script);
    }

    [Fact]
    public void Public_v2_package_spec_defines_component_runtime_boundary()
    {
        var spec = Read("specs", "requirements", "protocol-lab", "SPEC-PL-LAB-PACKAGE-V2.json");

        Assert.Contains("protocol-lab-package-v2", spec);
        Assert.Contains("\"status\": \"approved\"", spec);
        Assert.Contains("test-executor", spec);
        Assert.Contains("scenario-pack", spec);
        Assert.Contains("REQ-PL-LABPKG2-0007", spec);
        Assert.Contains("Component compatibility semantics", spec);
        Assert.Contains("reject incompatible component sets rather than substituting a different protocol lane", spec);
        Assert.Contains("package-relative entry manifests", spec);
        Assert.Contains("must not be rooted paths, URI paths, or contain traversal segments", spec);
        Assert.Contains("No legacy v2 aliases", spec);
        Assert.Contains("MUST use kind test-executor and providedTestExecutors", spec);
        Assert.Contains("MUST NOT use load-runner, load-tool package kinds, or providedLoadTools", spec);
        Assert.Contains("test-executor command artifact", spec);
        Assert.Contains("generic worker primitives", spec);
        Assert.Contains("effective manifest IDs", spec);
    }

    [Fact]
    public void Public_v2_package_doc_defines_per_package_compatibility_and_artifact_contract()
    {
        var doc = Read("docs", "lab", "package-v2.md");

        Assert.Contains("providedImplementations", doc);
        Assert.Contains("providedTestExecutors", doc);
        Assert.Contains("providedSuites", doc);
        Assert.Contains("Workers and controllers must reject incompatible component sets", doc);
        Assert.Contains("must not fall back to `managed-httpclient-h3-load`", doc);
        Assert.Contains("Runtime component packages", doc);
        Assert.Contains("package-relative paths", doc);
        Assert.Contains("path traversal segments are invalid", doc);
        Assert.Contains("`load-tool-command.txt`", doc);
        Assert.Contains("legacy `h2load-command.txt`", doc);
    }

    private static string Read(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([TestPaths.RepoRoot, .. pathParts]));
    }
}
