// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json.Nodes;
using Incursa.ProtocolLab.Adapter.Conformance;
using Incursa.ProtocolLab.Adapter.Contracts;

namespace Incursa.ProtocolLab.Tests;

public sealed class TestExecutorSchemaValidatorTests
{
    private static string SchemaRoot => Path.Combine(TestPaths.RepoRoot, "schemas", "test-executor", "v1");

    [Fact]
    public async Task Validator_accepts_good_request_and_response_shapes()
    {
        var validator = new TestExecutorSchemaValidator(SchemaRoot);
        var summary = new TestExecutorSessionSummary
        {
            SessionId = "session-001",
            State = TestExecutorSessionState.Running,
            TestId = "http.core.plaintext",
            ScenarioId = "http.core.plaintext",
            Protocol = "h3",
            RunId = "run-001",
            CellId = "cell-001"
        };

        Assert.True((await validator.ValidateObjectAsync("health", new TestExecutorHealthResponse
        {
            ExecutorIdentity = new TestExecutorIdentity { Id = "fixture-executor", Name = "Fixture Executor" },
            Status = TestExecutorHealthStatus.Ready,
            VersionCompatibility = new TestExecutorVersionCompatibility { ContractVersion = "test-executor-v1", CompatibleContractVersions = ["test-executor-v1"] },
            Capabilities =
            [
                new TestExecutorCapability { Id = "traffic.h3.basic", Status = TestExecutorCapabilityStatus.Supported }
            ]
        })).IsValid);

        Assert.True((await validator.ValidateObjectAsync("manifest", new TestExecutorManifestResponse
        {
            ExecutorIdentity = new TestExecutorIdentity { Id = "fixture-executor", Name = "Fixture Executor" },
            VersionCompatibility = new TestExecutorVersionCompatibility { ContractVersion = "test-executor-v1", CompatibleContractVersions = ["test-executor-v1"] },
            ClaimedCapabilities =
            [
                new TestExecutorCapability { Id = "traffic.h3.basic", Status = TestExecutorCapabilityStatus.Supported }
            ],
            SupportedTestSelectors =
            [
                new TestExecutorSelector { SelectorType = "test-id", Expression = "http.core.*" }
            ],
            SupportedScenarioSelectors =
            [
                new TestExecutorSelector { SelectorType = "scenario-id", Expression = "http.core.*" }
            ],
            SupportedProtocolFamilies = ["h3"],
            SupportedExecutionModes = ["single-cell"],
            RequiredTargetEndpointBindings =
            [
                new TestExecutorEndpointBinding { BindingId = "primary", Purpose = "test-endpoint", EndpointType = "h3", Protocols = ["h3"] }
            ],
            SupportedArtifactTypes =
            [
                new TestExecutorArtifactType { Type = "log", ProducedByStates = ["stopped"] }
            ],
            MetricsAvailability = new TestExecutorMetricsAvailability { Available = true, AvailableKinds = ["summary"] }
        })).IsValid);

        Assert.True((await validator.ValidateObjectAsync("session-create", new TestExecutorSessionCreateRequest
        {
            RequestedSessionId = "session-001",
            RunId = "run-001",
            CellId = "cell-001",
            SessionLabel = "fixture"
        })).IsValid);

        Assert.True((await validator.ValidateObjectAsync("session-resource", new TestExecutorSessionResource
        {
            Session = summary,
            Operation = new TestExecutorOperationResult { Category = TestExecutorOperationResultCategory.Succeeded }
        })).IsValid);

        Assert.True((await validator.ValidateObjectAsync("prepare", new TestExecutorPrepareRequest
        {
            TestId = "http.core.plaintext",
            ScenarioId = "http.core.plaintext",
            ScenarioVersion = "1.0",
            Protocol = "h3",
            TestDocument = ProtocolLabAdapterJson.SerializeValue(new { kind = "test" }),
            ScenarioDocument = ProtocolLabAdapterJson.SerializeValue(new { kind = "scenario" }),
            TargetEndpoints =
            [
                new TestExecutorTargetEndpoint
                {
                    BindingId = "primary",
                    EndpointId = "target-001",
                    Purpose = "test-endpoint",
                    Scheme = "https",
                    Protocol = "h3",
                    Host = "127.0.0.1",
                    Port = 4433
                }
            ],
            RunId = "run-001",
            CellId = "cell-001",
            ArtifactOutputExpectations =
            [
                new TestExecutorArtifactExpectation { ArtifactType = "log", Required = false }
            ]
        })).IsValid);

        Assert.True((await validator.ValidateObjectAsync("operation", new TestExecutorOperationResult
        {
            Category = TestExecutorOperationResultCategory.Succeeded,
            Message = "Succeeded."
        })).IsValid);

        Assert.True((await validator.ValidateObjectAsync("status", new TestExecutorStatusResponse
        {
            Session = summary,
            Operation = new TestExecutorOperationResult { Category = TestExecutorOperationResultCategory.Succeeded }
        })).IsValid);

        Assert.True((await validator.ValidateObjectAsync("metrics", new TestExecutorMetricsResponse
        {
            Session = summary,
            Availability = TestExecutorResourceAvailability.Available,
            Metrics =
            [
                new TestExecutorMetric { MetricId = "requests.total", Scope = "session", Value = ProtocolLabAdapterJson.SerializeValue(1) }
            ]
        })).IsValid);

        Assert.True((await validator.ValidateObjectAsync("artifacts", new TestExecutorArtifactsResponse
        {
            Session = summary,
            Availability = TestExecutorResourceAvailability.Available,
            Artifacts =
            [
                new TestExecutorArtifact { ArtifactId = "executor-log", ArtifactType = "log", Status = TestExecutorResourceAvailability.Available }
            ]
        })).IsValid);

        Assert.True((await validator.ValidateObjectAsync("problem", new TestExecutorProblemDetails
        {
            Type = "https://incursa.example/problems/fixture",
            Title = "Fixture problem",
            Status = 400,
            Code = "fixture"
        })).IsValid);
    }

    [Fact]
    public async Task Validator_rejects_missing_required_fields_and_invalid_enum_values()
    {
        var validator = new TestExecutorSchemaValidator(SchemaRoot);

        var missingRequiredFields = JsonNode.Parse("""{"status":"ready","versionCompatibility":{"contractVersion":"test-executor-v1","compatibleContractVersions":["test-executor-v1"]}}""")!.AsObject();
        var missingRequiredResult = await validator.ValidateJsonAsync("health", missingRequiredFields.ToJsonString());
        Assert.False(missingRequiredResult.IsValid);
        Assert.Contains(missingRequiredResult.Errors, error => error.Contains("executorIdentity", StringComparison.OrdinalIgnoreCase));

        var invalidEnum = JsonNode.Parse("""{"executorIdentity":{"id":"executor","name":"Executor"},"status":"bogus","versionCompatibility":{"contractVersion":"test-executor-v1","compatibleContractVersions":["test-executor-v1"]}}""")!.AsObject();
        var invalidEnumResult = await validator.ValidateJsonAsync("health", invalidEnum.ToJsonString());
        Assert.False(invalidEnumResult.IsValid);
        Assert.Contains(invalidEnumResult.Errors, error => error.Contains("status", StringComparison.OrdinalIgnoreCase));
    }
}
