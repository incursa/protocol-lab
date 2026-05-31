// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed class ScenarioValidationError
{
    public string Field { get; init; } = "";
    public string Message { get; init; } = "";

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Field) ? Message : $"{Field}: {Message}";
    }
}

public static class ScenarioValidator
{
    private static readonly HashSet<string> DisallowedIdWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "incursa", "kestrel", "msquic", "caddy", "nginx", "docker", "h2load", "oha"
    };

    public static IReadOnlyList<ScenarioValidationError> Validate(ScenarioDefinition scenario, string? filePath = null)
    {
        var errors = new List<ScenarioValidationError>();

        ValidateRequiredString(errors, nameof(scenario.Id), scenario.Id);
        ValidateRequiredString(errors, nameof(scenario.Title), scenario.GetTitle());
        ValidateRequiredString(errors, nameof(scenario.SchemaVersion), scenario.SchemaVersion);
        ValidateRequiredString(errors, nameof(scenario.Description), scenario.Description);
        ValidateRequiredString(errors, nameof(scenario.Protocol), scenario.Protocol);

        if (!string.IsNullOrWhiteSpace(scenario.Id))
        {
            ValidateScenarioId(scenario.Id, errors);
        }

        if (!string.IsNullOrWhiteSpace(scenario.Status))
        {
            var validStatuses = new[] { "draft", "stable", "experimental", "deprecated", "placeholder" };
            if (!validStatuses.Contains(scenario.Status, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(new ScenarioValidationError
                {
                    Field = nameof(scenario.Status),
                    Message = $"Invalid status '{scenario.Status}'. Valid values: {string.Join(", ", validStatuses)}"
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(scenario.Kind))
        {
            var validKinds = new[] { "workload", "protocolValidation", "protocol-validation", "interopValidation", "interop-validation", "diagnostic", "profile" };
            if (!validKinds.Contains(scenario.Kind, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(new ScenarioValidationError
                {
                    Field = nameof(scenario.Kind),
                    Message = $"Invalid kind '{scenario.Kind}'. Valid values: workload, protocolValidation, interopValidation, diagnostic, profile"
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(scenario.Layer))
        {
            var validLayers = new[] { "application", "protocol", "transport" };
            if (!validLayers.Contains(scenario.Layer, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(new ScenarioValidationError
                {
                    Field = nameof(scenario.Layer),
                    Message = $"Invalid layer '{scenario.Layer}'. Valid values: application, protocol, transport"
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(scenario.TrafficShape))
        {
            var validShapes = new[]
            {
                "requestResponse", "request-response",
                "upload", "download",
                "streamingDownload", "streaming-download",
                "streamingUpload", "streaming-upload",
                "bidirectionalStream", "bidirectional-stream",
                "datagram", "handshakeOnly", "handshake-only",
                "connectionLifecycle", "connection-lifecycle"
            };
            if (!validShapes.Contains(scenario.TrafficShape, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(new ScenarioValidationError
                {
                    Field = nameof(scenario.TrafficShape),
                    Message = $"Invalid trafficShape '{scenario.TrafficShape}'"
                });
            }
        }

        var effectiveRoles = scenario.GetEffectiveRoles();
        if (effectiveRoles.Count == 0 && scenario.Requires.Roles.Count == 0 && string.IsNullOrWhiteSpace(scenario.ImplementationRole))
        {
            errors.Add(new ScenarioValidationError
            {
                Field = "roles",
                Message = "At least one role must be specified"
            });
        }

        if (scenario.Requires.Protocols.Count == 0 && string.IsNullOrWhiteSpace(scenario.Protocol))
        {
            errors.Add(new ScenarioValidationError
            {
                Field = "requires.protocols",
                Message = "At least one protocol must be specified"
            });
        }

        if (scenario.Validation.Checks.Count == 0 && scenario.Validation.Required)
        {
            errors.Add(new ScenarioValidationError
            {
                Field = nameof(scenario.Validation),
                Message = "Validation checks are required"
            });
        }

        return errors;
    }

    private static void ValidateRequiredString(List<ScenarioValidationError> errors, string field, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ScenarioValidationError
            {
                Field = field,
                Message = $"'{field}' is required"
            });
        }
    }

    private static void ValidateScenarioId(string id, List<ScenarioValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var parts = id.Split('.');
        if (parts.Length < 2)
        {
            errors.Add(new ScenarioValidationError
            {
                Field = "id",
                Message = $"Scenario ID '{id}' must be in dotted format (e.g., http.plaintext)"
            });
        }

        foreach (var part in parts)
        {
            if (DisallowedIdWords.Contains(part))
            {
                errors.Add(new ScenarioValidationError
                {
                    Field = "id",
                    Message = $"Scenario ID '{id}' contains an implementation-specific word '{part}'. Scenario IDs must be implementation-neutral."
                });
                break;
            }
        }
    }
}
