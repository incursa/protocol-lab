// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Incursa.ProtocolLab.Adapter.Contracts;
using Newtonsoft.Json.Linq;
using NJsonSchema;

namespace Incursa.ProtocolLab.Adapter.Conformance;

public sealed class AdapterSchemaValidator
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
        ["endpoints"] = "endpoints-response.schema.json",
        ["metrics"] = "metrics-response.schema.json",
        ["artifacts"] = "artifacts-response.schema.json",
        ["problem"] = "problem-details.schema.json",
    };

    private readonly string schemaRootPath;
    private readonly ConcurrentDictionary<string, Lazy<Task<JsonSchema>>> cache = new(StringComparer.OrdinalIgnoreCase);

    public AdapterSchemaValidator(string schemaRootPath)
    {
        if (string.IsNullOrWhiteSpace(schemaRootPath))
        {
            throw new ArgumentException("A schema root path is required.", nameof(schemaRootPath));
        }

        this.schemaRootPath = schemaRootPath;
    }

    public Task<AdapterSchemaValidationResult> ValidateObjectAsync<T>(string schemaName, T value, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(value, ProtocolLabAdapterJson.Options);
        return ValidateJsonAsync(schemaName, payload, cancellationToken);
    }

    public async Task<AdapterSchemaValidationResult> ValidateJsonAsync(string schemaName, string payload, CancellationToken cancellationToken = default)
    {
        var schemaPath = ResolveSchemaPath(schemaName);
        JsonNode? node;

        try
        {
            node = JsonNode.Parse(payload);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return new AdapterSchemaValidationResult(schemaName, schemaPath, false, [$"Malformed JSON payload: {ex.Message}"], payload);
        }

        if (node is not JsonObject root)
        {
            return new AdapterSchemaValidationResult(schemaName, schemaPath, false, ["The adapter payload must be a JSON object."], payload);
        }

        var diagnostics = ValidatePayload(schemaName, root);
        return new AdapterSchemaValidationResult(schemaName, schemaPath, diagnostics.Count == 0, diagnostics, payload);
    }

    public string ResolveSchemaPath(string schemaName)
    {
        if (!SchemaFiles.TryGetValue(schemaName, out var fileName))
        {
            throw new ArgumentOutOfRangeException(nameof(schemaName), schemaName, "Unknown adapter schema name.");
        }

        return Path.Combine(schemaRootPath, fileName);
    }

    private Task<JsonSchema> LoadSchemaAsync(string schemaPath, CancellationToken cancellationToken)
    {
        return cache.GetOrAdd(
                schemaPath,
                static path => new Lazy<Task<JsonSchema>>(() => LoadSchemaCoreAsync(path, CancellationToken.None)))
            .Value;
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
            "endpoints" => ValidateEndpointsResponse(root),
            "metrics" => ValidateMetricsResponse(root),
            "artifacts" => ValidateArtifactsResponse(root),
            "problem" => ValidateProblemDetails(root),
            _ => throw new ArgumentOutOfRangeException(nameof(schemaName), schemaName, "Unknown adapter schema name.")
        };
    }

    private static List<string> ValidateHealthResponse(JsonObject root)
    {
        var errors = new List<string>();
        ValidateIdentity(root, "adapterIdentity", "health.adapterIdentity", errors);
        ValidateEnumString(root, "status", ["ready", "degraded", "not_ready", "unavailable", "unsupported"], "health.status", errors);
        ValidateVersionCompatibility(root, "versionCompatibility", "health.versionCompatibility", errors);
        ValidateCapabilityArray(root, "capabilities", "health.capabilities", errors);
        return errors;
    }

    private static List<string> ValidateManifestResponse(JsonObject root)
    {
        var errors = new List<string>();
        ValidateIdentity(root, "adapterIdentity", "manifest.adapterIdentity", errors);
        ValidateIdentity(root, "implementationIdentity", "manifest.implementationIdentity", errors);
        ValidateVersionCompatibility(root, "versionCompatibility", "manifest.versionCompatibility", errors);
        ValidateStringArray(root, "supportedRoles", "manifest.supportedRoles", errors, required: true);
        ValidateCapabilityArray(root, "claimedCapabilities", "manifest.claimedCapabilities", errors);
        ValidateScenarioSelectorArray(root, "supportedScenarioSelectors", "manifest.supportedScenarioSelectors", errors);
        ValidateEndpointTypeArray(root, "supportedEndpointTypes", "manifest.supportedEndpointTypes", errors);
        ValidateArtifactTypeArray(root, "supportedArtifactTypes", "manifest.supportedArtifactTypes", errors);
        ValidateMetricsAvailability(root, "metricsAvailability", "manifest.metricsAvailability", errors);
        ValidateStringArray(root, "defaultResponseContentTypes", "manifest.defaultResponseContentTypes", errors, required: false);
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
        ValidateRequiredString(root, "scenarioId", "prepare.scenarioId", errors);
        ValidateRequiredString(root, "scenarioVersion", "prepare.scenarioVersion", errors);
        ValidateRequiredString(root, "role", "prepare.role", errors);
        if (!root.ContainsKey("scenarioDocument") || root["scenarioDocument"] is null)
        {
            errors.Add("prepare.scenarioDocument is required.");
        }
        ValidateEndpointBindingArray(root, "requestedEndpointBindings", "prepare.requestedEndpointBindings", errors);
        ValidateRequiredString(root, "runId", "prepare.runId", errors);
        ValidateRequiredString(root, "cellId", "prepare.cellId", errors);
        ValidateArtifactExpectationArray(root, "artifactOutputExpectations", "prepare.artifactOutputExpectations", errors);
        return errors;
    }

    private static List<string> ValidateOperationResult(JsonObject root)
    {
        var errors = new List<string>();
        ValidateEnumString(root, "category", ["succeeded", "pending", "unsupported", "rejected", "failed"], "operation.category", errors, required: true);
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
        ValidateReadinessSnapshot(root, "readiness", "status.readiness", errors);
        ValidateHealthSnapshot(root, "health", "status.health", errors);
        ValidateOperation(root, "operation", "status.operation", errors, required: false);
        return errors;
    }

    private static List<string> ValidateEndpointsResponse(JsonObject root)
    {
        var errors = new List<string>();
        ValidateSessionSummary(root, "session", "endpoints.session", errors);
        ValidateEndpointArray(root, "endpoints", "endpoints.endpoints", errors);
        ValidateOperation(root, "operation", "endpoints.operation", errors, required: false);
        return errors;
    }

    private static List<string> ValidateMetricsResponse(JsonObject root)
    {
        var errors = new List<string>();
        ValidateSessionSummary(root, "session", "metrics.session", errors);
        ValidateEnumString(root, "availability", ["available", "partial", "unavailable", "unsupported"], "metrics.availability", errors, required: true);
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
        ValidateEnumString(root, "availability", ["available", "partial", "unavailable", "unsupported"], "artifacts.availability", errors, required: true);
        ValidateArtifactArray(root, "artifacts", "artifacts.artifacts", errors);
        ValidateOperation(root, "operation", "artifacts.operation", errors, required: false);
        return errors;
    }

    private static List<string> ValidateProblemDetails(JsonObject root)
    {
        var errors = new List<string>();
        ValidateRequiredString(root, "type", "problem.type", errors);
        ValidateRequiredString(root, "title", "problem.title", errors);
        ValidateRequiredNumber(root, "status", "problem.status", errors);
        ValidateOptionalString(root, "detail", "problem.detail", errors);
        ValidateOptionalString(root, "instance", "problem.instance", errors);
        ValidateOptionalString(root, "code", "problem.code", errors);
        ValidateEnumString(root, "adapterStatus", ["ready", "degraded", "not_ready", "unavailable", "unsupported"], "problem.adapterStatus", errors, required: false);
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
        ValidateOptionalString(versionCompatibility, "implementationVersion", $"{path}.implementationVersion", errors);
    }

    private static void ValidateCapabilityArray(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (!TryGetArray(root, propertyName, out var array, required: true, path, errors))
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

    private static void ValidateScenarioSelectorArray(JsonObject root, string propertyName, string path, ICollection<string> errors)
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
        }
    }

    private static void ValidateEndpointTypeArray(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (!TryGetArray(root, propertyName, out var array, required: true, path, errors))
        {
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject endpointType)
            {
                errors.Add($"{path}[{index}] must be an object.");
                continue;
            }

            ValidateRequiredString(endpointType, "type", $"{path}[{index}].type", errors);
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
        ValidateOptionalBoolean(metricsAvailability, "sessionMetricsAvailable", $"{path}.sessionMetricsAvailable", errors);
        ValidateOptionalBoolean(metricsAvailability, "endpointMetricsAvailable", $"{path}.endpointMetricsAvailable", errors);
        ValidateOptionalBoolean(metricsAvailability, "processMetricsAvailable", $"{path}.processMetricsAvailable", errors);
        ValidateOptionalBoolean(metricsAvailability, "containerMetricsAvailable", $"{path}.containerMetricsAvailable", errors);
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
        ValidateEnumString(summary, "state", ["created", "preparing", "prepared", "starting", "running", "ready", "stopping", "stopped", "failed", "unsupported", "disposed"], $"{path}.state", errors);
        ValidateOptionalString(summary, "scenarioId", $"{path}.scenarioId", errors);
        ValidateOptionalString(summary, "scenarioVersion", $"{path}.scenarioVersion", errors);
        ValidateOptionalString(summary, "role", $"{path}.role", errors);
        ValidateOptionalString(summary, "runId", $"{path}.runId", errors);
        ValidateOptionalString(summary, "cellId", $"{path}.cellId", errors);
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

    private static void ValidateReadinessSnapshot(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (root[propertyName] is not JsonObject readiness)
        {
            errors.Add($"{path} is required and must be an object.");
            return;
        }

        ValidateEnumString(readiness, "status", ["unknown", "not_ready", "ready", "unsupported", "failed"], $"{path}.status", errors);
        ValidateOptionalString(readiness, "message", $"{path}.message", errors);
        ValidateStringArray(readiness, "warnings", $"{path}.warnings", errors, required: false);
    }

    private static void ValidateHealthSnapshot(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (root[propertyName] is not JsonObject health)
        {
            errors.Add($"{path} is required and must be an object.");
            return;
        }

        ValidateEnumString(health, "status", ["ready", "degraded", "not_ready", "unavailable", "unsupported"], $"{path}.status", errors);
        ValidateOptionalString(health, "message", $"{path}.message", errors);
    }

    private static void ValidateEndpointArray(JsonObject root, string propertyName, string path, ICollection<string> errors)
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

            ValidateRequiredString(endpoint, "endpointId", $"{path}[{index}].endpointId", errors);
            ValidateRequiredString(endpoint, "purpose", $"{path}[{index}].purpose", errors);
            ValidateRequiredString(endpoint, "scheme", $"{path}[{index}].scheme", errors);
            ValidateRequiredString(endpoint, "protocol", $"{path}[{index}].protocol", errors);
            ValidateRequiredString(endpoint, "host", $"{path}[{index}].host", errors);
            ValidateRequiredInteger(endpoint, "port", 0, 65535, $"{path}[{index}].port", errors);
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
            ValidateOptionalBoolean(binding, "required", $"{path}[{index}].required", errors);
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
            if (array[index] is not JsonValue value || !value.TryGetValue<string>(out _))
            {
                errors.Add($"{path}[{index}] must be a string.");
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

    private static void ValidateRequiredNumber(JsonObject root, string propertyName, string path, ICollection<string> errors)
    {
        if (!root.TryGetPropertyValue(propertyName, out var value) || value is null)
        {
            errors.Add($"{path} is required.");
            return;
        }

        if (value is not JsonValue jsonValue || !jsonValue.TryGetValue<double>(out _))
        {
            errors.Add($"{path} must be a number.");
        }
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

    private static async Task<JsonSchema> LoadSchemaCoreAsync(string schemaPath, CancellationToken cancellationToken)
    {
        var schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken);
        schemaJson = await ExpandCommonDefinitionsAsync(schemaPath, schemaJson, cancellationToken);
        return await JsonSchema.FromJsonAsync(schemaJson, schemaPath, cancellationToken);
    }

    private static async Task<string> ExpandCommonDefinitionsAsync(string schemaPath, string schemaJson, CancellationToken cancellationToken)
    {
        if (Path.GetFileName(schemaPath).Equals("common.schema.json", StringComparison.OrdinalIgnoreCase))
        {
            return schemaJson;
        }

        var schemaNode = JsonNode.Parse(schemaJson)!.AsObject();
        var commonSchemaPath = Path.Combine(Path.GetDirectoryName(schemaPath)!, "common.schema.json");
        var commonSchemaJson = await File.ReadAllTextAsync(commonSchemaPath, cancellationToken);
        var commonNode = JsonNode.Parse(commonSchemaJson)!.AsObject();

        if (commonNode["$defs"] is JsonObject commonDefs)
        {
            var rootDefs = schemaNode["definitions"] as JsonObject ?? new JsonObject();
            foreach (var (key, value) in commonDefs)
            {
                if (!rootDefs.ContainsKey(key))
                {
                    rootDefs[key] = value?.DeepClone();
                }
            }

            schemaNode["definitions"] = rootDefs;
        }

        schemaNode.Remove("$defs");
        RewriteCommonReferences(schemaNode);
        return schemaNode.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void RewriteCommonReferences(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToArray())
                {
                    if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        if (text.StartsWith("./common.schema.json#/$defs/", StringComparison.OrdinalIgnoreCase))
                        {
                            obj[property.Key] = text["./common.schema.json#/$defs/".Length..].Insert(0, "#/definitions/");
                            continue;
                        }

                        if (text.StartsWith("common.schema.json#/$defs/", StringComparison.OrdinalIgnoreCase))
                        {
                            obj[property.Key] = text["common.schema.json#/$defs/".Length..].Insert(0, "#/definitions/");
                            continue;
                        }

                        if (text.StartsWith("#/$defs/", StringComparison.OrdinalIgnoreCase))
                        {
                            obj[property.Key] = text["#/$defs/".Length..].Insert(0, "#/definitions/");
                            continue;
                        }
                    }

                    if (property.Value is not null)
                    {
                        RewriteCommonReferences(property.Value);
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        RewriteCommonReferences(item);
                    }
                }

                break;
        }
    }
}
