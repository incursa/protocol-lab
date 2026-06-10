// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Adapter.Conformance;
using Incursa.ProtocolLab.Cli;

namespace Incursa.ProtocolLab.Tests;

public sealed class PublicConformanceHarnessTests
{
    [Theory]
    [InlineData("neutral-test-executor")]
    [InlineData("neutral-adapter-implementation")]
    [InlineData("neutral-scenario-pack")]
    public async Task Public_contract_package_fixtures_pass_package_v2_conformance(string fixtureName)
    {
        var validator = new PackageConformanceValidator();
        var report = await validator.ValidateAsync(
            Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "packages", fixtureName),
            new PackageConformanceOptions
            {
                PackageSchemaPath = Path.Combine(TestPaths.RepoRoot, "schemas", "package", "v2", "package.schema.json"),
                TestExecutorSchemaRootPath = Path.Combine(TestPaths.RepoRoot, "schemas", "test-executor", "v1"),
                ScenarioSchemaPath = Path.Combine(TestPaths.RepoRoot, "schemas", "scenario.schema.json")
            });

        Assert.Equal(PackageConformanceOutcome.Passed, report.Outcome);
        Assert.All(report.Steps, step => Assert.Equal(PackageConformanceOutcome.Passed, step.Outcome));
    }

    [Fact]
    public async Task Cli_package_conformance_command_accepts_neutral_test_executor_fixture()
    {
        var exitCode = await ProtocolLabCommand.RunAsync(
        [
            "conformance",
            "package",
            "--package",
            Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "packages", "neutral-test-executor"),
            "--root",
            TestPaths.RepoRoot
        ]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Public_docs_show_local_third_party_conformance_commands()
    {
        var quickstart = Read("docs", "quickstart.md");
        var adapterConformance = Read("docs", "runner", "adapter-conformance.md");
        var testExecutorConformance = Read("docs", "runner", "test-executor-conformance.md");
        var packageV2 = Read("docs", "lab", "package-v2.md");
        var fixtureReadme = Read("fixtures", "public-contracts", "README.md");

        Assert.Contains("conformance package --package", quickstart, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("conformance adapter --base-url", adapterConformance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("conformance test-executor --base-url", testExecutorConformance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("neutral-test-executor", packageV2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("neutral-adapter-implementation", fixtureReadme, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([TestPaths.RepoRoot, .. pathParts]));
    }
}
