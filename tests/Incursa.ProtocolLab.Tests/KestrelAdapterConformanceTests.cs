// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Adapter.Conformance;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Tests.Fixtures.KestrelAdapterLab;

namespace Incursa.ProtocolLab.Tests;

[Collection(RunnerContractFixtureLabCollection.Name)]
public sealed class KestrelAdapterConformanceTests
{
    private static string SchemaRoot => Path.Combine(TestPaths.RepoRoot, "schemas", "adapter", "v1");

    [Fact]
    public async Task Real_kestrel_adapter_passes_the_conformance_suite_for_supported_scenarios()
    {
        await using var host = await KestrelAdapterProcessHost.StartAsync();

        var report = await RunSuiteAsync(host.Client.BaseAddress!, "fixture.kestrel.success", "h1");

        Assert.Equal(AdapterConformanceOutcome.Passed, report.Outcome);
        Assert.Contains(report.Steps, step => step.Step == "health" && step.Outcome == AdapterConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "manifest" && step.Outcome == AdapterConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "endpoints" && step.Outcome == AdapterConformanceOutcome.Passed);
        Assert.Contains(report.Steps, step => step.Step == "delete-idempotent" && step.Outcome == AdapterConformanceOutcome.Passed);
    }

    [Fact]
    public async Task Real_kestrel_adapter_reports_unsupported_scenarios_structurally()
    {
        await using var host = await KestrelAdapterProcessHost.StartAsync();

        var report = await RunSuiteAsync(host.Client.BaseAddress!, "fixture.kestrel.unsupported", "h1");

        Assert.Equal(AdapterConformanceOutcome.Unsupported, report.Outcome);
        Assert.Contains(report.Steps, step => step.Step == "prepare-unsupported" && step.Outcome == AdapterConformanceOutcome.Unsupported);
    }

    private static async Task<AdapterConformanceReport> RunSuiteAsync(Uri controlPlaneBaseUrl, string scenarioId, string protocol)
    {
        var suite = new AdapterConformanceSuite();
        var options = new AdapterConformanceOptions
        {
            SchemaRootPath = SchemaRoot,
            SupportedContractVersion = "v1",
            Timeout = TimeSpan.FromSeconds(60)
        };

        return await suite.RunAsync(
            controlPlaneBaseUrl,
            new AdapterConformanceScenario
            {
                ScenarioId = scenarioId,
                ScenarioVersion = "1.0",
                Role = "server",
                Protocol = protocol,
                RunId = "kestrel-adapter-conformance",
                CellId = "kestrel-adapter-conformance",
                SessionLabel = "kestrel-adapter-conformance",
                RequestedEndpointBindings =
                [
                    new AdapterEndpointBinding
                    {
                        BindingId = "primary",
                        Purpose = "test-endpoint",
                        EndpointType = protocol
                    }
                ],
                ArtifactOutputExpectations =
                [
                    new AdapterArtifactExpectation
                    {
                        ArtifactType = "stdout",
                        Required = true
                    }
                ]
            },
            options);
    }
}
