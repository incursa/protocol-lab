// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;

namespace Incursa.ProtocolLab.Adapter.Conformance;

public enum TestExecutorConformanceOutcome
{
    Passed,
    Unsupported,
    ContractFailure,
    InfrastructureFailure,
    MalformedResponse,
    Timeout,
}

public sealed record TestExecutorConformanceScenario
{
    public string TestId { get; init; } = "test-executor.conformance";

    public string ScenarioId { get; init; } = "test-executor.conformance";

    public string ScenarioVersion { get; init; } = "1.0";

    public string Protocol { get; init; } = "h3";

    public JsonElement TestDocument { get; init; } = JsonSerializer.SerializeToElement(
        new Dictionary<string, string> { ["kind"] = "test-executor-conformance-test" },
        JsonSerializerOptions.Web);

    public JsonElement ScenarioDocument { get; init; } = JsonSerializer.SerializeToElement(
        new Dictionary<string, string> { ["kind"] = "test-executor-conformance-scenario" },
        JsonSerializerOptions.Web);

    public string RunId { get; init; } = "test-executor-conformance";

    public string CellId { get; init; } = "test-executor-conformance";

    public string SessionLabel { get; init; } = "test-executor-conformance";

    public IReadOnlyList<TestExecutorTargetEndpoint> TargetEndpoints { get; init; } =
    [
        new TestExecutorTargetEndpoint
        {
            BindingId = "primary",
            EndpointId = "target-001",
            Purpose = "test-endpoint",
            Scheme = "https",
            Protocol = "h3",
            Host = "127.0.0.1",
            Port = 4433,
            Path = "/"
        }
    ];

    public IReadOnlyList<TestExecutorArtifactExpectation> ArtifactOutputExpectations { get; init; } =
    [
        new TestExecutorArtifactExpectation
        {
            ArtifactType = "log",
            Required = false
        }
    ];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record TestExecutorConformanceOptions
{
    public string SchemaRootPath { get; init; } = "";

    public string SupportedContractVersion { get; init; } = "test-executor-v1";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    public bool ValidateSchemas { get; init; } = true;

    public bool RequireAvailableMetrics { get; init; }

    public bool RequireAvailableArtifacts { get; init; }

    public bool ValidateDeleteIdempotency { get; init; } = true;

    public Func<HttpMessageHandler>? HttpMessageHandlerFactory { get; set; }
}

public sealed record TestExecutorConformanceStepResult(
    string Step,
    TestExecutorConformanceOutcome Outcome,
    string Message,
    string? SchemaName = null,
    string? SchemaPath = null,
    HttpStatusCode? StatusCode = null,
    IReadOnlyList<string>? Diagnostics = null);

public sealed record TestExecutorConformanceReport(
    string ControlPlaneBaseUrl,
    TestExecutorConformanceOutcome Outcome,
    IReadOnlyList<TestExecutorConformanceStepResult> Steps,
    string? SessionId = null,
    IReadOnlyList<string>? Warnings = null);

public sealed record TestExecutorSchemaValidationResult(
    string SchemaName,
    string SchemaPath,
    bool IsValid,
    IReadOnlyList<string> Errors,
    string? Payload = null);
