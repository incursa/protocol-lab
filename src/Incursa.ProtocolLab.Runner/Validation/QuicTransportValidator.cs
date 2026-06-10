// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal static class QuicTransportValidator
{
    private static readonly HashSet<string> LoadValidatedScenarioIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "quic.transport.multiplex.100x64kb",
        "quic.transport.duplex-streams"
    };

    public static async Task<ScenarioValidationResult> ValidateAsync(
        RunCell cell,
        string? baseUrl,
        ArtifactPaths? paths = null,
        string certificateMode = "")
    {
        var support = ScenarioSupport.Evaluate(cell.Implementation, cell.Scenario, cell.Protocol);
        if (!support.IsSupported)
        {
            return CreateResult(cell, ValidationStatus.Unsupported, support.Reason);
        }

        if (cell.Scenario.QuicTransport is null)
        {
            return CreateResult(cell, ValidationStatus.Unsupported,
                "QUIC transport validation is only available for scenarios with quicTransport metadata.");
        }

        if (IsCatalogRawTransportScenario(cell) && !LoadValidatedScenarioIds.Contains(cell.Scenario.Id))
        {
            return CreateResult(cell, ValidationStatus.Unsupported,
                $"Raw QUIC execution for scenario '{cell.Scenario.Id}' is not enabled yet. Enabled load-validated scenarios: {string.Join(", ", LoadValidatedScenarioIds.OrderBy(static id => id, StringComparer.OrdinalIgnoreCase))}.");
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return CreateResult(cell, ValidationStatus.NotApplicable,
                "No --base-url supplied; QUIC endpoint validation was not run.",
                warnings: ["Definition compatibility was checked only."]);
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "quic", StringComparison.OrdinalIgnoreCase))
        {
            return CreateResult(cell, ValidationStatus.Unsupported,
                $"QUIC transport validation requires a quic:// endpoint, got '{baseUrl}'.");
        }

        var observations = new List<ValidationObservation>
        {
            new()
            {
                Category = "endpoint",
                Description = "QUIC endpoint scheme",
                ExpectedValue = "quic",
                ActualValue = uri.Scheme
            },
            new()
            {
                Category = "endpoint",
                Description = "QUIC endpoint host",
                ExpectedValue = uri.Host,
                ActualValue = uri.Host
            },
            new()
            {
                Category = "endpoint",
                Description = "QUIC endpoint port",
                ExpectedValue = uri.Port.ToString(CultureInfo.InvariantCulture),
                ActualValue = uri.Port.ToString(CultureInfo.InvariantCulture)
            }
        };

        var warnings = new List<string> { BenchmarkEvidenceReasons.AdapterBackedTarget };
        var errors = new List<string>();
        var proofArtifacts = new List<ValidationProofArtifact>();
        var transport = cell.Scenario.QuicTransport;

        if (uri.Port <= 0)
        {
            errors.Add("QUIC endpoint base URL must include a valid port.");
        }

        if (transport.ConnectionCount <= 0)
        {
            errors.Add("quicTransport.connectionCount must be greater than zero.");
        }

        if (transport.StreamCount < 0)
        {
            errors.Add("quicTransport.streamCount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(transport.StreamType))
        {
            warnings.Add("quicTransport.streamType was not specified.");
        }

        if (string.IsNullOrWhiteSpace(transport.PayloadDirection))
        {
            warnings.Add("quicTransport.payloadDirection was not specified.");
        }

        if (!string.IsNullOrWhiteSpace(certificateMode))
        {
            observations.Add(new ValidationObservation
            {
                Category = "tls",
                Description = "Certificate mode",
                ActualValue = certificateMode
            });
        }

        if (paths is not null)
        {
            await ValidateTargetExecutionArtifactAsync(paths, baseUrl, observations, proofArtifacts, errors);
            await ValidateAdapterEndpointArtifactAsync(paths, uri, observations, proofArtifacts, errors);
        }

        if (errors.Count > 0)
        {
            return CreateResult(
                cell,
                ValidationStatus.Failed,
                "QUIC transport endpoint validation failed.",
                errors: errors,
                warnings: warnings,
                observations: observations,
                proofArtifacts: proofArtifacts);
        }

        var summary = IsCatalogRawTransportScenario(cell)
            ? "QUIC transport endpoint matched adapter metadata; raw load-tool validation is required before accepting benchmark data."
            : proofArtifacts.Count > 0
                ? "QUIC transport endpoint matched the adapter endpoint metadata."
                : "QUIC transport endpoint URI was structurally valid.";

        return CreateResult(
            cell,
            ValidationStatus.Passed,
            summary,
            warnings: warnings,
            observations: observations,
            proofArtifacts: proofArtifacts);
    }

    public static ScenarioValidationResult ValidateLoadMetrics(
        RunCell cell,
        ScenarioValidationResult endpointValidation,
        HttpMetrics metrics,
        bool parsedMetricsAvailable,
        string loadToolStdoutPath,
        string loadToolStderrPath,
        int? qlogFileCount)
    {
        if (!IsCatalogRawTransportScenario(cell) || !LoadValidatedScenarioIds.Contains(cell.Scenario.Id))
        {
            return endpointValidation;
        }

        var observations = endpointValidation.Observations.ToList();
        var proofArtifacts = endpointValidation.ProofArtifacts.ToList();
        var warnings = endpointValidation.Warnings.ToList();
        var errors = endpointValidation.Errors.ToList();

        proofArtifacts.Add(new ValidationProofArtifact
        {
            Name = "load-tool-stdout",
            Path = loadToolStdoutPath,
            Category = "load-tool",
            Description = "Raw QUIC load-tool stdout for this attempt."
        });
        proofArtifacts.Add(new ValidationProofArtifact
        {
            Name = "load-tool-stderr",
            Path = loadToolStderrPath,
            Category = "load-tool",
            Description = "Raw QUIC load-tool stderr for this attempt."
        });

        if ((qlogFileCount ?? 0) <= 0)
        {
            warnings.Add("Raw QUIC qlog artifacts were not produced by the selected load-tool/target pair; qlog evidence is unavailable for this cell.");
        }

        if (!parsedMetricsAvailable)
        {
            errors.Add("Raw QUIC load-tool output did not contain parseable metrics.");
        }

        var expectedBatch = BuildExpectedBatch(cell);
        AddObservation(observations, "raw-quic", "Expected bytes per complete scenario batch", expectedBatch.ExpectedBytes?.ToString(CultureInfo.InvariantCulture));
        AddObservation(observations, "raw-quic", "Expected streams per complete scenario batch", expectedBatch.ExpectedStreams.ToString(CultureInfo.InvariantCulture));
        AddObservation(observations, "raw-quic", "Actual bytes sent", metrics.BytesSent?.ToString(CultureInfo.InvariantCulture));
        AddObservation(observations, "raw-quic", "Actual bytes received", metrics.BytesReceived?.ToString(CultureInfo.InvariantCulture));
        AddObservation(observations, "raw-quic", "Actual completed streams", metrics.CompletedStreams?.ToString(CultureInfo.InvariantCulture) ?? metrics.SuccessfulRequests?.ToString(CultureInfo.InvariantCulture));
        AddObservation(observations, "raw-quic", "Failed requests", (metrics.FailedRequests ?? 0).ToString(CultureInfo.InvariantCulture));
        AddObservation(observations, "raw-quic", "Timeout requests", (metrics.TimeoutRequests ?? 0).ToString(CultureInfo.InvariantCulture));

        if ((metrics.FailedRequests ?? 0) != 0)
        {
            errors.Add($"Raw QUIC failed request count was {metrics.FailedRequests.GetValueOrDefault().ToString(CultureInfo.InvariantCulture)}, expected 0.");
        }

        if ((metrics.TimeoutRequests ?? 0) != 0)
        {
            errors.Add($"Raw QUIC timeout request count was {metrics.TimeoutRequests.GetValueOrDefault().ToString(CultureInfo.InvariantCulture)}, expected 0.");
        }

        if (expectedBatch.ExpectedStreams > 0)
        {
            var completedStreams = metrics.CompletedStreams ?? metrics.SuccessfulRequests;
            if (!completedStreams.HasValue)
            {
                errors.Add("Raw QUIC completed stream count was not reported.");
            }
            else if (completedStreams.Value <= 0 || completedStreams.Value % expectedBatch.ExpectedStreams != 0)
            {
                errors.Add($"Raw QUIC completed streams were {completedStreams.Value.ToString(CultureInfo.InvariantCulture)}, expected a positive multiple of {expectedBatch.ExpectedStreams.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        if (expectedBatch.ExpectedBytes.HasValue && expectedBatch.ExpectedBytes.Value > 0)
        {
            var bytesSent = metrics.BytesSent;
            var bytesReceived = metrics.BytesReceived;
            if (!bytesSent.HasValue || !bytesReceived.HasValue)
            {
                errors.Add("Raw QUIC bytes sent and bytes received must both be reported.");
            }
            else
            {
                var totalBytes = bytesSent.Value + bytesReceived.Value;
                if (IsBidirectional(cell) && bytesSent.Value != bytesReceived.Value)
                {
                    errors.Add($"Raw QUIC bytes sent ({bytesSent.Value.ToString(CultureInfo.InvariantCulture)}) did not match bytes received ({bytesReceived.Value.ToString(CultureInfo.InvariantCulture)}).");
                }

                if (totalBytes <= 0 || totalBytes % expectedBatch.ExpectedBytes.Value != 0)
                {
                    errors.Add($"Raw QUIC total bytes were {totalBytes.ToString(CultureInfo.InvariantCulture)}, expected a positive multiple of {expectedBatch.ExpectedBytes.Value.ToString(CultureInfo.InvariantCulture)}.");
                }
            }
        }

        if (errors.Count > 0)
        {
            return endpointValidation with
            {
                Status = ValidationStatus.Failed,
                Summary = "Raw QUIC load-tool validation gates failed.",
                Errors = errors.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                Observations = observations,
                ProofArtifacts = proofArtifacts
            };
        }

        return endpointValidation with
        {
            Status = ValidationStatus.Passed,
            Summary = "Raw QUIC endpoint and load-tool validation gates passed.",
            Errors = [],
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Observations = observations,
            ProofArtifacts = proofArtifacts
        };
    }

    private static async Task ValidateTargetExecutionArtifactAsync(
        ArtifactPaths paths,
        string baseUrl,
        List<ValidationObservation> observations,
        List<ValidationProofArtifact> proofArtifacts,
        List<string> errors)
    {
        if (!File.Exists(paths.TargetExecutionJson))
        {
            return;
        }

        proofArtifacts.Add(new ValidationProofArtifact
        {
            Name = "target-execution",
            Path = paths.TargetExecutionJson,
            Category = "execution"
        });

        try
        {
            var targetExecution = ResultJson.Deserialize<TargetExecutionResult>(await File.ReadAllTextAsync(paths.TargetExecutionJson));
            if (targetExecution is null)
            {
                errors.Add("Target execution artifact could not be parsed.");
            }
            else if (!string.IsNullOrWhiteSpace(targetExecution.TargetEffectiveBaseUrl) &&
                !string.Equals(targetExecution.TargetEffectiveBaseUrl, baseUrl, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Target effective base URL '{targetExecution.TargetEffectiveBaseUrl}' did not match the validated QUIC endpoint '{baseUrl}'.");
            }

            if (targetExecution is not null && !string.IsNullOrWhiteSpace(targetExecution.TargetContract))
            {
                observations.Add(new ValidationObservation
                {
                    Category = "target",
                    Description = "Target contract",
                    ActualValue = targetExecution.TargetContract
                });
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
        {
            errors.Add($"Target execution artifact could not be parsed: {ex.Message}");
        }
    }

    private static async Task ValidateAdapterEndpointArtifactAsync(
        ArtifactPaths paths,
        Uri uri,
        List<ValidationObservation> observations,
        List<ValidationProofArtifact> proofArtifacts,
        List<string> errors)
    {
        if (!File.Exists(paths.AdapterEndpointsJson))
        {
            return;
        }

        proofArtifacts.Add(new ValidationProofArtifact
        {
            Name = "adapter-endpoints",
            Path = paths.AdapterEndpointsJson,
            Category = "adapter"
        });

        try
        {
            var endpoints = JsonSerializer.Deserialize<AdapterEndpointsResponse>(
                await File.ReadAllTextAsync(paths.AdapterEndpointsJson),
                ProtocolLabAdapterJson.Options);

            var endpoint = endpoints?.Endpoints.FirstOrDefault(candidate =>
                string.Equals(candidate.Scheme, "quic", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Protocol, "quic", StringComparison.OrdinalIgnoreCase));

            if (endpoint is null)
            {
                errors.Add("Adapter did not expose a quic endpoint.");
                return;
            }

            observations.Add(new ValidationObservation
            {
                Category = "adapter",
                Description = "Adapter endpoint scheme",
                ExpectedValue = "quic",
                ActualValue = endpoint.Scheme
            });
            observations.Add(new ValidationObservation
            {
                Category = "adapter",
                Description = "Adapter endpoint protocol",
                ExpectedValue = "quic",
                ActualValue = endpoint.Protocol
            });

            if (!string.Equals(endpoint.Host, uri.Host, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Adapter QUIC endpoint host '{endpoint.Host}' did not match the validated endpoint host '{uri.Host}'.");
            }

            if (endpoint.Port != uri.Port)
            {
                errors.Add($"Adapter QUIC endpoint port '{endpoint.Port}' did not match the validated endpoint port '{uri.Port}'.");
            }

            if (endpoint.Extensions.TryGetValue("transport", out var transportValue) &&
                transportValue.ValueKind == JsonValueKind.String &&
                !string.Equals(transportValue.GetString(), "udp", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Adapter QUIC endpoint transport '{transportValue.GetString()}' was not udp.");
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
        {
            errors.Add($"Adapter endpoints artifact could not be parsed: {ex.Message}");
        }
    }

    private static ScenarioValidationResult CreateResult(
        RunCell cell,
        ValidationStatus status,
        string summary,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<ValidationObservation>? observations = null,
        IReadOnlyList<ValidationProofArtifact>? proofArtifacts = null)
    {
        return new ScenarioValidationResult
        {
            ScenarioId = cell.Scenario.Id,
            TargetId = cell.Implementation.Id,
            AdapterId = "",
            Protocol = cell.Protocol,
            Status = status,
            Summary = summary,
            Errors = errors ?? [],
            Warnings = warnings ?? [],
            Observations = observations ?? [],
            ProofArtifacts = proofArtifacts ?? []
        };
    }

    private static bool IsCatalogRawTransportScenario(RunCell cell)
    {
        return string.Equals(cell.Scenario.Family, "quic.transport", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBidirectional(RunCell cell)
    {
        return string.Equals(cell.Scenario.QuicTransport?.PayloadDirection, "bidirectional", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(cell.Scenario.QuicTransport?.PayloadDirection, "bidirectional-stream", StringComparison.OrdinalIgnoreCase);
    }

    private static RawQuicExpectedBatch BuildExpectedBatch(RunCell cell)
    {
        var transport = cell.Scenario.QuicTransport;
        if (transport is null)
        {
            return new RawQuicExpectedBatch(null, 0);
        }

        var expectedStreams = Math.Max(0, cell.StreamsPerConnection);
        var payloadBytes = Math.Max(0, transport.PayloadSizeBytes ?? 0);
        var directionMultiplier = IsBidirectional(cell) ? 2 : 1;
        var computedExpectedBytes = expectedStreams == 0
            ? 0
            : (long)expectedStreams * payloadBytes * directionMultiplier;
        var expectedBytes = computedExpectedBytes > 0
            ? computedExpectedBytes
            : transport.ExpectedBytes;

        return new RawQuicExpectedBatch(expectedBytes, expectedStreams);
    }

    private static void AddObservation(List<ValidationObservation> observations, string category, string description, string? actualValue)
    {
        observations.Add(new ValidationObservation
        {
            Category = category,
            Description = description,
            ActualValue = actualValue
        });
    }

    private sealed record RawQuicExpectedBatch(long? ExpectedBytes, int ExpectedStreams);
}
