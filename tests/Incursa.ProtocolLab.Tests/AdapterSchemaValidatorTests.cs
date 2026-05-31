// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Incursa.ProtocolLab.Adapter.Conformance;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Tests.Fixtures.AdapterContractLab;

namespace Incursa.ProtocolLab.Tests;

public sealed class AdapterSchemaValidatorTests
{
    private static string SchemaRoot => Path.Combine(TestPaths.RepoRoot, "schemas", "adapter", "v1");

    [Fact]
    public async Task Validator_accepts_good_request_and_response_shapes()
    {
        await using var host = await FakeAdapterHost.StartAsync(FakeAdapterHostOptions.CreateDefault() with
        {
            UseRealNetworkServer = true,
            ControlPlanePort = 0
        });

        var validator = new AdapterSchemaValidator(SchemaRoot);
        var client = new ProtocolLabAdapterClient(host.Client);

        var health = await client.GetHealthAsync();
        Assert.True((await validator.ValidateObjectAsync("health", health)).IsValid);

        var manifest = await client.GetManifestAsync();
        Assert.True((await validator.ValidateObjectAsync("manifest", manifest)).IsValid);

        using var scenarioDocument = JsonDocument.Parse("""{"kind":"fixture"}""");
        var sessionCreateRequest = new AdapterSessionCreateRequest
        {
            RequestedSessionId = "session-001",
            RunId = "run-001",
            CellId = "cell-001",
            SessionLabel = "fixture"
        };
        Assert.True((await validator.ValidateObjectAsync("session-create", sessionCreateRequest)).IsValid);

        var session = await client.CreateSessionAsync(sessionCreateRequest);
        Assert.True((await validator.ValidateObjectAsync("session-resource", session)).IsValid);

        var prepareRequest = new AdapterPrepareRequest
        {
            ScenarioId = "success",
            ScenarioVersion = "1.0",
            Role = "server",
            ScenarioDocument = scenarioDocument.RootElement.Clone(),
            RequestedEndpointBindings =
            [
                new AdapterEndpointBinding
                {
                    BindingId = "primary",
                    Purpose = "test-endpoint",
                    EndpointType = "quic"
                }
            ],
            RunId = "run-001",
            CellId = "cell-001",
            ArtifactOutputExpectations =
            [
                new AdapterArtifactExpectation
                {
                    ArtifactType = "log",
                    Required = true
                }
            ]
        };
        Assert.True((await validator.ValidateObjectAsync("prepare", prepareRequest)).IsValid);

        var prepare = await client.PrepareAsync(session.Session.SessionId, prepareRequest);
        Assert.True((await validator.ValidateObjectAsync("operation", prepare)).IsValid);

        var start = await client.StartAsync(session.Session.SessionId);
        Assert.True((await validator.ValidateObjectAsync("operation", start)).IsValid);

        var status = await client.GetStatusAsync(session.Session.SessionId);
        Assert.True((await validator.ValidateObjectAsync("status", status)).IsValid);

        var endpoints = await client.GetEndpointsAsync(session.Session.SessionId);
        Assert.True((await validator.ValidateObjectAsync("endpoints", endpoints)).IsValid);

        var metrics = await client.GetMetricsAsync(session.Session.SessionId);
        Assert.True((await validator.ValidateObjectAsync("metrics", metrics)).IsValid);

        var artifacts = await client.GetArtifactsAsync(session.Session.SessionId);
        Assert.True((await validator.ValidateObjectAsync("artifacts", artifacts)).IsValid);

        var stop = await client.StopAsync(session.Session.SessionId);
        Assert.True((await validator.ValidateObjectAsync("operation", stop)).IsValid);

        var delete = await client.DeleteSessionAsync(session.Session.SessionId);
        Assert.True((await validator.ValidateObjectAsync("operation", delete)).IsValid);
    }

    [Fact]
    public async Task Validator_accepts_problem_details_payloads()
    {
        await using var host = await FakeAdapterHost.StartAsync(FakeAdapterHostOptions.CreateDefault() with
        {
            ManifestBehavior = FakeAdapterManifestBehavior.Problem
        });

        var validator = new AdapterSchemaValidator(SchemaRoot);
        var client = new ProtocolLabAdapterClient(host.Client);

        var ex = await Assert.ThrowsAsync<ProtocolLabAdapterProblemException>(() => client.GetManifestAsync());
        var validation = await validator.ValidateJsonAsync("problem", ex.RawContent);

        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task Validator_rejects_missing_required_fields_and_invalid_enum_values()
    {
        var validator = new AdapterSchemaValidator(SchemaRoot);

        var missingRequiredFields = JsonNode.Parse("""{"status":"ready","versionCompatibility":{"contractVersion":"v1","compatibleContractVersions":["v1"]}}""")!.AsObject();
        var missingRequiredResult = await validator.ValidateJsonAsync("health", missingRequiredFields.ToJsonString());
        Assert.False(missingRequiredResult.IsValid);
        Assert.Contains(missingRequiredResult.Errors, error => error.Contains("adapterIdentity", StringComparison.OrdinalIgnoreCase));

        var invalidEnum = JsonNode.Parse("""{"adapterIdentity":{"id":"adapter","name":"Adapter"},"status":"bogus","versionCompatibility":{"contractVersion":"v1","compatibleContractVersions":["v1"]}}""")!.AsObject();
        var invalidEnumResult = await validator.ValidateJsonAsync("health", invalidEnum.ToJsonString());
        Assert.False(invalidEnumResult.IsValid);
        Assert.Contains(invalidEnumResult.Errors, error => error.Contains("status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Validator_rejects_invalid_endpoint_and_capability_shapes()
    {
        var validator = new AdapterSchemaValidator(SchemaRoot);

        var endpoints = JsonNode.Parse("""
        {
          "session": { "sessionId": "session-001", "state": "running" },
          "endpoints": [
            {
              "endpointId": "endpoint-001",
              "purpose": "test",
              "scheme": "quic",
              "protocol": "quic",
              "port": 4433
            }
          ]
        }
        """)!.AsObject();
        var endpointsResult = await validator.ValidateJsonAsync("endpoints", endpoints.ToJsonString());
        Assert.False(endpointsResult.IsValid);
        Assert.Contains(endpointsResult.Errors, error => error.Contains("host", StringComparison.OrdinalIgnoreCase));

        var manifest = JsonNode.Parse("""
        {
          "adapterIdentity": { "id": "adapter", "name": "Adapter" },
          "implementationIdentity": { "id": "implementation", "name": "Implementation" },
          "versionCompatibility": { "contractVersion": "v1", "compatibleContractVersions": ["v1"] },
          "supportedRoles": ["server"],
          "claimedCapabilities": [
            { "id": "session-lifecycle", "status": "bogus" }
          ],
          "supportedScenarioSelectors": [
            { "selectorType": "scenario-id", "expression": "fixture.*" }
          ],
          "supportedEndpointTypes": [
            { "type": "quic", "protocols": ["quic"] }
          ],
          "supportedArtifactTypes": [
            { "type": "log" }
          ],
          "metricsAvailability": { "available": true }
        }
        """)!.AsObject();
        var manifestResult = await validator.ValidateJsonAsync("manifest", manifest.ToJsonString());
        Assert.False(manifestResult.IsValid);
        Assert.Contains(manifestResult.Errors, error => error.Contains("status", StringComparison.OrdinalIgnoreCase));
    }
}
