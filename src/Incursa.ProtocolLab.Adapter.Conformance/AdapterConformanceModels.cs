// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;

namespace Incursa.ProtocolLab.Adapter.Conformance;

public enum AdapterConformanceOutcome
{
    Passed,
    Unsupported,
    ContractFailure,
    InfrastructureFailure,
    MalformedResponse,
    Timeout,
}

public sealed record AdapterConformanceScenario
{
    public string ScenarioId { get; init; } = "adapter.conformance";

    public string ScenarioVersion { get; init; } = "1.0";

    public string Role { get; init; } = "server";

    public string Protocol { get; init; } = "h1";

    public string RunId { get; init; } = "adapter-conformance";

    public string CellId { get; init; } = "adapter-conformance";

    public string SessionLabel { get; init; } = "adapter-conformance";

    public IReadOnlyList<AdapterEndpointBinding> RequestedEndpointBindings { get; init; } = [];

    public IReadOnlyList<AdapterArtifactExpectation> ArtifactOutputExpectations { get; init; } = [];

    public Dictionary<string, JsonElement> Extensions { get; init; } = [];
}

public sealed record AdapterConformanceOptions
{
    public string SchemaRootPath { get; init; } = "";

    public string SupportedContractVersion { get; init; } = "v1";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    public bool ValidateSchemas { get; init; } = true;

    public bool ValidateInvalidLifecycleTransition { get; init; } = true;

    public bool ValidateDeleteIdempotency { get; init; } = true;

    public Func<HttpMessageHandler>? HttpMessageHandlerFactory { get; set; }
}

public sealed record AdapterConformanceStepResult(
    string Step,
    AdapterConformanceOutcome Outcome,
    string Message,
    string? SchemaName = null,
    string? SchemaPath = null,
    HttpStatusCode? StatusCode = null,
    IReadOnlyList<string>? Diagnostics = null);

public sealed record AdapterConformanceReport(
    string ControlPlaneBaseUrl,
    AdapterConformanceOutcome Outcome,
    IReadOnlyList<AdapterConformanceStepResult> Steps,
    string? SessionId = null,
    IReadOnlyList<string>? Warnings = null);

public sealed record AdapterSchemaValidationResult(
    string SchemaName,
    string SchemaPath,
    bool IsValid,
    IReadOnlyList<string> Errors,
    string? Payload = null);
