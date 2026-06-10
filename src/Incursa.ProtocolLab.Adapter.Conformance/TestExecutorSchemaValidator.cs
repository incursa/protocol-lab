// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Incursa.ProtocolLab.Adapter.Contracts;

namespace Incursa.ProtocolLab.Adapter.Conformance;

public sealed class TestExecutorSchemaValidator
{
    private static readonly IReadOnlyDictionary<string, string> SchemaFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["health"] = "health-response.schema.json",
        ["manifest"] = "manifest.schema.json",
        ["session-create"] = "session-create-request.schema.json",
        ["session-resource"] = "session-resource.schema.json",
        ["prepare"] = "prepare-request.schema.json",
        ["operation"] = "operation-response.schema.json",
        ["status"] = "status-response.schema.json",
        ["metrics"] = "metrics-response.schema.json",
        ["artifacts"] = "artifacts-response.schema.json",
        ["problem"] = "problem-details.schema.json",
    };

    private readonly string schemaRootPath;

    public TestExecutorSchemaValidator(string schemaRootPath)
    {
        if (string.IsNullOrWhiteSpace(schemaRootPath))
        {
            throw new ArgumentException("A schema root path is required.", nameof(schemaRootPath));
        }

        this.schemaRootPath = schemaRootPath;
    }

    public Task<TestExecutorSchemaValidationResult> ValidateObjectAsync<T>(string schemaName, T value, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(value, ProtocolLabAdapterJson.Options);
        return ValidateJsonAsync(schemaName, payload, cancellationToken);
    }

    public Task<TestExecutorSchemaValidationResult> ValidateJsonAsync(string schemaName, string payload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var schemaPath = ResolveSchemaPath(schemaName);
        JsonNode? node;

        try
        {
            node = JsonNode.Parse(payload);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return Task.FromResult(new TestExecutorSchemaValidationResult(schemaName, schemaPath, false, [$"Malformed JSON payload: {ex.Message}"], payload));
        }

        if (node is not JsonObject root)
        {
            return Task.FromResult(new TestExecutorSchemaValidationResult(schemaName, schemaPath, false, ["The test executor payload must be a JSON object."], payload));
        }

        var diagnostics = ValidatePayload(schemaName, root);
        return Task.FromResult(new TestExecutorSchemaValidationResult(schemaName, schemaPath, diagnostics.Count == 0, diagnostics, payload));
    }

    public string ResolveSchemaPath(string schemaName)
    {
        if (!SchemaFiles.TryGetValue(schemaName, out var fileName))
        {
            throw new ArgumentOutOfRangeException(nameof(schemaName), schemaName, "Unknown test executor schema name.");
        }

        return Path.Combine(schemaRootPath, fileName);
    }

    private static List<string> ValidatePayload(string schemaName, JsonObject root)
    {
        return schemaName.ToLowerInvariant() switch
        {
            "health" => ValidateHealthResponse(root),
            "manifest" => ValidateManifestResponse(root),
            "session-create" => ValidateSessionCreateRequest(root),
            "session-resource" => ValidateSessionResource(root),
            "prepare" => ValidatePrepareRequest(root),
            "operation" => ValidateOperationResult(root),
            "status" => ValidateStatusResponse(root),
            "metrics" => ValidateMetricsResponse(root),
            "artifacts" => ValidateArtifactsResponse(root),
            "problem" => ValidateProblemDetails(root),
            _ => throw new ArgumentOutOfRangeException(nameof(schemaName), schemaName, "Unknown test executor schema name.")
        };
    }

    private static List<string> ValidateHealthResponse(JsonObject root)
    {
        var errors = new List<string>();
        ValidateIdentity(root, "executorIdentity", "health.executorIdentity", errors);
        ValidateEnumString(root, "status", ["ready", "degraded", "not_ready", "unavailable", "unsupported"], "health.status", errors);
        ValidateVersionCompatibility(root, "versionCompatibility", "health.versionCompatibility", errors);
        ValidateCapabilityArray(root, "capabilities", "health.capabilities", errors, required: false);
        return errors;
    }

    private static List<string> ValidateManifestResponse(JsonObject root)
    {
        var errors = new List<string>();
        ValidateIdentity(root, "executorIdentity", "manifest.executorIdentity", errors);
        ValidateVersionCompatibility(root, "versionCompatibility", "manifest.versionCompatibility", errors);
        ValidateCapabilityArray(root, "claimedCapabilities", "manifest.claimedCapabilities", errors, required: true);
        ValidateSelectorArray(root, "supportedTestSelectors", "manifest.supportedTestSelectors", errors);
        ValidateSelectorArray(root, "supportedScenarioSelectors", "manifest.supportedScenarioSelectors", errors);
        ValidateStringArray(root, "supportedProtocolFamilies", "manifest.supportedProtocolFamilies", errors, required: true);
        ValidateStringArray(root, "supportedExecutionModes", "manifest.supportedExecutionModes", errors, required: true);
        ValidateEndpointBindingArray(root, "requiredTargetEndpointBindings", "manifest.requiredTargetEndpointBindings", errors);
        ValidateArtifactTypeArray(root, "supportedArtifactTypes", "manifest.supportedArtifactTypes", errors);
        ValidateMetricsAvailability(root, "metricsAvailability", "manifest.metricsAvailability", errors);
        return errors;
    }

    private static List<string> ValidateSessionCreateRequest(JsonObject root)
    {
        var errors = new List<string>();
        ValidateOptionalString(root, "requestedSessionId", "session-create.requestedSessionId", errors);
        ValidateOptionalString(root, "runId", "session-create.runId", errors);
        ValidateOptionalString(root, "cellId", "session-create.cellId", errors);
        ValidateOptionalString(root, "sessionLabel", "session-create.sessionLabel", errors);
        return errors;
    }

    private static List<string> ValidateSessionResource(JsonObject root)
    {
        var errors = new List<string>();
        ValidateSessionSummary(root, "session", "session-resource.session", errors);
        ValidateOperation(root, "operation", "session-resource.operation", errors, required: false);
        return errors;
    }

    private static List<string> ValidatePrepareRequest(JsonObject root)
    {
        var errors = new List<string>();
        ValidateRequiredString(root, "testId", "prepare.testId", errors);
        ValidateRequiredString(root, "scenarioId", "prepare.scenarioId", errors);
        ValidateRequiredString(root, "scenarioVersion", "prepare.scenarioVersion", errors);
        ValidateRequiredString(root, "protocol", "prepare.protocol", errors);
        if (!root.ContainsKey("testDocument") || root["testDocument"] is null)
        {
            errors.Add("prepare.testDocument is required.");
        }

        if (!root.ContainsKey("scenarioDocument") || root["scenarioDocument"] is null)
        {
            errors.Add("prepare.scenarioDocument is required.");
        }

        ValidateTargetEndpointArray(root, "targetEndpoints", "prepare.targetEndpoints", errors);
        ValidateRequiredString(root, "runId", "prepare.runId", errors);
        ValidateRequiredString(root, "cellId", "prepare.cellId", errors);
        ValidateArtifactExpectationArray(root, "artifactOutputExpectations", "prepare.artifactOutputExpectations", errors);
        return errors;
    }

    private static List<string> ValidateOperationResult(JsonObject root)
    {
        var errors = new List<string>();
        ValidateEnumString(root, "category", ["succeeded", "pending", "unsupported", "rejected", "failed"], "operation.category", errors);
        ValidateOptionalString(root, "message", "operation.message", errors);
        ValidateOptionalString(root, "code", "operation.code", errors);
        ValidateOptionalBoolean(root, "retryable", "operation.retryable", errors);
        ValidateStringArray(root, "warnings", "operation.warnings", errors, required: false);
        return errors;
    }

    private static List<string> ValidateStatusResponse(JsonObject root)
    {
        var errors = new List<string>();
        ValidateSessionSummary(root, "session", "status.session", errors);
        ValidateOperation(root, "operation", "status.operation", errors, required: false);
        return errors;
    }

    private static List<string> ValidateMetricsResponse(JsonObject root)
    {
        var errors = new List<string>();
        ValidateSessionSummary(root, "session", "metrics.session", errors);
        ValidateEnumString(root, "availability", ["available", "partial", "unavailable", "unsupported"], "metrics.availability", errors);
        ValidateOptionalDateTime(root, "capturedAt", "metrics.capturedAt", errors);
        ValidateMetricArray(root, "metrics", "metrics.metrics", errors);
        ValidateStringArray(root, "notes", "metrics.notes", errors, required: false);
        ValidateOperation(root, "operation", "metrics.operation", errors, required: false);
        return errors;
    }

    private static List<string> ValidateArtifactsResponse(JsonObject root)
    {
        var errors = new List<string>();
        ValidateSessionSummary(root, "session", "artifacts.session", errors);
        ValidateEnumString(root, "availability", ["available", "partial", "unavailable", "unsupported"], "artifacts.availability", errors);
        ValidateArtifactArray(root, "artifacts", "artifacts.artifacts", errors);
        ValidateOperation(root, "operation", "artifacts.operation", errors, required: false);
        return errors;
    }

    private static List<string> ValidateProblemDetails(JsonObject root)
    {
        var errors = new List<string>();
        ValidateRequiredString(root, "type", "problem.type", errors);
        ValidateRequiredString(root, "title", "problem.title", errors);
        ValidateRequiredInteger(root, "status", 100, 599, "problem.status", errors);
        ValidateOptionalString(root, "detail", "problem.detail", errors);
        ValidateOptionalString(root, "instance", "problem.instance", errors);
        ValidateOptionalString(root, "code", "problem.code", errors);
        ValidateEnumString(root, "executorStatus", ["ready", "degraded", "not_ready", "unavailable", "unsupported"], "problem.executorStatus", errors, required: false);
        ValidateOptionalString(root, "operation", "problem.operation", errors);
        ValidateOptionalString(root, "sessionId", "problem.sessionId", errors);
        ValidateOptionalString(root, "unsupportedReason", "problem.unsupportedReason", errors);
        ValidateOptionalBoolean(root, "retryable", "problem.retryable", errors);
        return errors;
    }

    private static void ValidateIdentity(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (root[propertyName] is not JsonObject identity)
        {
            errors.Add($"{path} is required and must be an object.");
            return;
        }

        ValidateRequiredString(identity, "id", $"{path}.id", errors);
        ValidateRequiredString(identity, "name", $"{path}.name", errors);
        ValidateOptionalString(identity, "version", $"{path}.version", errors);
        ValidateOptionalString(identity, "revision", $"{path}.revision", errors);
        ValidateOptionalString(identity, "vendor", $"{path}.vendor", errors);
    }

    private static void ValidateVersionCompatibility(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (root[propertyName] is not JsonObject versionCompatibility)
        {
            errors.Add($"{path} is required and must be an object.");
            return;
        }

        ValidateRequiredString(versionCompatibility, "contractVersion", $"{path}.contractVersion", errors);
        ValidateStringArray(versionCompatibility, "compatibleContractVersions", $"{path}.compatibleContractVersions", errors, required: true);
        ValidateOptionalString(versionCompatibility, "executorVersion", $"{path}.executorVersion", errors);
    }

    private static void ValidateCapabilityArray(JsonObject root, string propertyName, string path, ICollection<string> errors, bool required)
    {
        if (!TryGetArray(root, propertyName, out var array, required, path, errors))
        {
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject capability)
            {
                errors.Add($"{path}[{index}] must be an object.");
                continue;
            }

            ValidateRequiredString(capability, "id", $"{path}[{index}].id", errors);
            ValidateEnumString(capability, "status", ["supported", "conditional", "partial", "experimental", "unsupported"], $"{path}[{index}].status", errors);
            ValidateOptionalString(capability, "version", $"{path}[{index}].version", errors);
            ValidateOptionalString(capability, "mode", $"{path}[{index}].mode", errors);
            ValidateOptionalString(capability, "description", $"{path}[{index}].description", errors);
        }
    }

    private static void ValidateSelectorArray(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (!TryGetArray(root, propertyName, out var array, required: true, path, errors))
        {
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject selector)
            {
                errors.Add($"{path}[{index}] must be an object.");
                continue;
            }

            ValidateRequiredString(selector, "selectorType", $"{path}[{index}].selectorType", errors);
            ValidateRequiredString(selector, "expression", $"{path}[{index}].expression", errors);
            ValidateOptionalString(selector, "description", $"{path}[{index}].description", errors);
        }
    }

    private static void ValidateEndpointBindingArray(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (!TryGetArray(root, propertyName, out var array, required: true, path, errors))
        {
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject binding)
            {
                errors.Add($"{path}[{index}] must be an object.");
                continue;
            }

            ValidateRequiredString(binding, "bindingId", $"{path}[{index}].bindingId", errors);
            ValidateRequiredString(binding, "purpose", $"{path}[{index}].purpose", errors);
            ValidateRequiredString(binding, "endpointType", $"{path}[{index}].endpointType", errors);
            ValidateStringArray(binding, "protocols", $"{path}[{index}].protocols", errors, required: false);
            ValidateOptionalBoolean(binding, "required", $"{path}[{index}].required", errors);
        }
    }

    private static void ValidateArtifactTypeArray(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (!TryGetArray(root, propertyName, out var array, required: true, path, errors))
        {
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject artifactType)
            {
                errors.Add($"{path}[{index}] must be an object.");
                continue;
            }

            ValidateRequiredString(artifactType, "type", $"{path}[{index}].type", errors);
            ValidateOptionalString(artifactType, "description", $"{path}[{index}].description", errors);
            ValidateStringArray(artifactType, "producedByStates", $"{path}[{index}].producedByStates", errors, required: false);
        }
    }

    private static void ValidateMetricsAvailability(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (root[propertyName] is not JsonObject metricsAvailability)
        {
            errors.Add($"{path} is required and must be an object.");
            return;
        }

        ValidateRequiredBoolean(metricsAvailability, "available", $"{path}.available", errors);
        ValidateStringArray(metricsAvailability, "availableKinds", $"{path}.availableKinds", errors, required: false);
    }

    private static void ValidateSessionSummary(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (root[propertyName] is not JsonObject summary)
        {
            errors.Add($"{path} is required and must be an object.");
            return;
        }

        ValidateRequiredString(summary, "sessionId", $"{path}.sessionId", errors);
        ValidateEnumString(summary, "state", ["created", "preparing", "prepared", "starting", "running", "stopping", "stopped", "failed", "unsupported", "disposed"], $"{path}.state", errors);
        ValidateOptionalString(summary, "testId", $"{path}.testId", errors);
        ValidateOptionalString(summary, "scenarioId", $"{path}.scenarioId", errors);
        ValidateOptionalString(summary, "protocol", $"{path}.protocol", errors);
        ValidateOptionalString(summary, "runId", $"{path}.runId", errors);
        ValidateOptionalString(summary, "cellId", $"{path}.cellId", errors);
        ValidateStringArray(summary, "warnings", $"{path}.warnings", errors, required: false);
    }

    private static void ValidateOperation(JsonObject root, string propertyName, string path, ICollection<string> errors, bool required)
    {
        if (root[propertyName] is null)
        {
            if (required)
            {
                errors.Add($"{path} is required and must be an object.");
            }

            return;
        }

        if (root[propertyName] is not JsonObject operation)
        {
            errors.Add($"{path} must be an object.");
            return;
        }

        ValidateEnumString(operation, "category", ["succeeded", "pending", "unsupported", "rejected", "failed"], $"{path}.category", errors);
        ValidateOptionalString(operation, "message", $"{path}.message", errors);
        ValidateOptionalString(operation, "code", $"{path}.code", errors);
        ValidateOptionalBoolean(operation, "retryable", $"{path}.retryable", errors);
        ValidateStringArray(operation, "warnings", $"{path}.warnings", errors, required: false);
    }

    private static void ValidateTargetEndpointArray(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (!TryGetArray(root, propertyName, out var array, required: true, path, errors))
        {
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject endpoint)
            {
                errors.Add($"{path}[{index}] must be an object.");
                continue;
            }

            ValidateRequiredString(endpoint, "bindingId", $"{path}[{index}].bindingId", errors);
            ValidateRequiredString(endpoint, "endpointId", $"{path}[{index}].endpointId", errors);
            ValidateRequiredString(endpoint, "purpose", $"{path}[{index}].purpose", errors);
            ValidateRequiredString(endpoint, "scheme", $"{path}[{index}].scheme", errors);
            ValidateRequiredString(endpoint, "protocol", $"{path}[{index}].protocol", errors);
            ValidateRequiredString(endpoint, "host", $"{path}[{index}].host", errors);
            ValidateRequiredInteger(endpoint, "port", 0, 65535, $"{path}[{index}].port", errors);
            ValidateOptionalString(endpoint, "path", $"{path}[{index}].path", errors);
            ValidateOptionalString(endpoint, "authority", $"{path}[{index}].authority", errors);
        }
    }

    private static void ValidateArtifactExpectationArray(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (!TryGetArray(root, propertyName, out var array, required: true, path, errors))
        {
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject expectation)
            {
                errors.Add($"{path}[{index}] must be an object.");
                continue;
            }

            ValidateRequiredString(expectation, "artifactType", $"{path}[{index}].artifactType", errors);
            ValidateOptionalBoolean(expectation, "required", $"{path}[{index}].required", errors);
            ValidateOptionalString(expectation, "destinationHint", $"{path}[{index}].destinationHint", errors);
            ValidateOptionalString(expectation, "contentType", $"{path}[{index}].contentType", errors);
        }
    }

    private static void ValidateMetricArray(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (!TryGetArray(root, propertyName, out var array, required: true, path, errors))
        {
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject metric)
            {
                errors.Add($"{path}[{index}] must be an object.");
                continue;
            }

            ValidateRequiredString(metric, "metricId", $"{path}[{index}].metricId", errors);
            ValidateRequiredString(metric, "scope", $"{path}[{index}].scope", errors);
            if (!metric.ContainsKey("value"))
            {
                errors.Add($"{path}[{index}].value is required.");
            }

            ValidateOptionalString(metric, "unit", $"{path}[{index}].unit", errors);
            ValidateOptionalDateTime(metric, "capturedAt", $"{path}[{index}].capturedAt", errors);
            ValidateOptionalString(metric, "notes", $"{path}[{index}].notes", errors);
        }
    }

    private static void ValidateArtifactArray(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (!TryGetArray(root, propertyName, out var array, required: true, path, errors))
        {
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject artifact)
            {
                errors.Add($"{path}[{index}] must be an object.");
                continue;
            }

            ValidateRequiredString(artifact, "artifactId", $"{path}[{index}].artifactId", errors);
            ValidateRequiredString(artifact, "artifactType", $"{path}[{index}].artifactType", errors);
            ValidateEnumString(artifact, "status", ["available", "partial", "unavailable", "unsupported"], $"{path}[{index}].status", errors);
            ValidateOptionalString(artifact, "path", $"{path}[{index}].path", errors);
            ValidateOptionalString(artifact, "uri", $"{path}[{index}].uri", errors);
            ValidateOptionalString(artifact, "contentType", $"{path}[{index}].contentType", errors);
            ValidateOptionalBoolean(artifact, "final", $"{path}[{index}].final", errors);
        }
    }

    private static void ValidateStringArray(JsonObject root, string propertyName, string path, ICollection<string> errors, bool required)
    {
        if (!TryGetArray(root, propertyName, out var array, required, path, errors))
        {
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
            {
                errors.Add($"{path}[{index}] must be a non-empty string.");
            }
        }
    }

    private static void ValidateEnumString(JsonObject root, string propertyName, IReadOnlyCollection<string> allowed, string path, ICollection<string> errors, bool required = true)
    {
        if (!TryGetString(root, propertyName, out var value, required, path, errors))
        {
            return;
        }

        if (!allowed.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"{path} must be one of: {string.Join(", ", allowed)}.");
        }
    }

    private static void ValidateRequiredString(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        _ = TryGetString(root, propertyName, out _, required: true, path, errors);
    }

    private static void ValidateOptionalString(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        _ = TryGetString(root, propertyName, out _, required: false, path, errors);
    }

    private static void ValidateRequiredBoolean(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        _ = TryGetBoolean(root, propertyName, out _, required: true, path, errors);
    }

    private static void ValidateOptionalBoolean(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        _ = TryGetBoolean(root, propertyName, out _, required: false, path, errors);
    }

    private static void ValidateRequiredInteger(JsonObject root, string propertyName, int minimum, int maximum, string path, ICollection<string> errors)
    {
        if (!root.TryGetPropertyValue(propertyName, out var value) || value is null)
        {
            errors.Add($"{path} is required.");
            return;
        }

        if (value is not JsonValue jsonValue || !jsonValue.TryGetValue<int>(out var number))
        {
            errors.Add($"{path} must be an integer.");
            return;
        }

        if (number < minimum || number > maximum)
        {
            errors.Add($"{path} must be between {minimum} and {maximum}.");
        }
    }

    private static void ValidateOptionalDateTime(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (!root.TryGetPropertyValue(propertyName, out var value) || value is null)
        {
            return;
        }

        if (value is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out var text) || !DateTimeOffset.TryParse(text, out _))
        {
            errors.Add($"{path} must be an ISO 8601 date-time string.");
        }
    }

    private static bool TryGetString(JsonObject root, string propertyName, out string? value, bool required, string path, ICollection<string> errors)
    {
        value = null;
        if (!root.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            if (required)
            {
                errors.Add($"{path} is required.");
            }

            return false;
        }

        if (node is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out value))
        {
            value = null;
            errors.Add($"{path} must be a string.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(value) && required)
        {
            errors.Add($"{path} must not be empty.");
            return false;
        }

        return true;
    }

    private static bool TryGetBoolean(JsonObject root, string propertyName, out bool value, bool required, string path, ICollection<string> errors)
    {
        value = default;
        if (!root.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            if (required)
            {
                errors.Add($"{path} is required.");
            }

            return false;
        }

        if (node is not JsonValue jsonValue || !jsonValue.TryGetValue<bool>(out value))
        {
            value = default;
            errors.Add($"{path} must be a boolean.");
            return false;
        }

        return true;
    }

    private static bool TryGetArray(JsonObject root, string propertyName, out JsonArray array, bool required, string path, ICollection<string> errors)
    {
        if (!root.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            if (required)
            {
                errors.Add($"{path} is required.");
            }

            array = [];
            return !required;
        }

        if (node is not JsonArray jsonArray)
        {
            errors.Add($"{path} must be an array.");
            array = [];
            return false;
        }

        array = jsonArray;
        return true;
    }
}
