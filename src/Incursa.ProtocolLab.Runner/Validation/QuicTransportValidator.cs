// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Model;
using Incursa.Quic;

namespace Incursa.ProtocolLab.Runner;

internal static class QuicTransportValidator
{
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
                "QUIC transport validation is only available for quic.transport scenarios.");
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
                ExpectedValue = uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ActualValue = uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        };

        var warnings = new List<string>();
        var errors = new List<string>();
        var proofArtifacts = new List<ValidationProofArtifact>();

        if (uri.Port <= 0)
        {
            errors.Add("QUIC endpoint base URL must include a valid port.");
        }

        var transport = cell.Scenario.QuicTransport;
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

        warnings.Add(BenchmarkEvidenceReasons.AdapterBackedTarget);

        var skipSmoke = paths is null ||
            cell.Implementation.Id.StartsWith("fixture-", StringComparison.OrdinalIgnoreCase);

        if (paths is null)
        {
            return CreateResult(
                cell,
                ValidationStatus.Passed,
                "QUIC transport endpoint URI was structurally valid.",
                warnings: warnings,
                observations: observations);
        }

        if (!skipSmoke && !QuicConnection.IsSupported)
        {
            return CreateResult(
                cell,
                ValidationStatus.InfrastructureFailure,
                "Incursa.Quic is not supported on this machine.",
                warnings: warnings,
                observations: observations);
        }

        if (!skipSmoke)
        {
            try
            {
                await RunTransportSmokeAsync(cell, uri, transport, certificateMode, warnings, observations);
            }
            catch (Exception ex) when (ex is AuthenticationException or IOException or OperationCanceledException or QuicException or SocketException or NotSupportedException or InvalidOperationException)
            {
                warnings.Add($"QUIC transport smoke failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        else
        {
            warnings.Add("QUIC smoke validation was skipped for fixture-backed implementation metadata.");
        }

        if (paths is not null)
        {
            if (File.Exists(paths.TargetExecutionJson))
            {
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

            if (File.Exists(paths.AdapterEndpointsJson))
            {
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
                    }
                    else
                    {
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
                }
                catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
                {
                    errors.Add($"Adapter endpoints artifact could not be parsed: {ex.Message}");
                }
            }
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

        var summary = proofArtifacts.Count > 0
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

    private static async Task RunTransportSmokeAsync(
        RunCell cell,
        Uri uri,
        QuicTransportSpec transport,
        string certificateMode,
        List<string> warnings,
        List<ValidationObservation> observations)
    {
        var sampleConnections = Math.Clamp(Math.Max(cell.Connections, 1), 1, 2);
        if (string.Equals(transport.StreamType, "none", StringComparison.OrdinalIgnoreCase))
        {
            sampleConnections = Math.Max(sampleConnections, 2);
        }
        var sampleStreams = string.Equals(transport.StreamType, "none", StringComparison.OrdinalIgnoreCase)
            ? 0
            : Math.Clamp(Math.Max(cell.StreamsPerConnection, 1), 1, 4);
        var payloadSize = Math.Max(0, transport.PayloadSizeBytes ?? 0);
        var payload = CreateDeterministicBytes(payloadSize);
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var connectionOptions = CreateConnectionOptions(uri, certificateMode);

        observations.Add(new ValidationObservation
        {
            Category = "quic",
            Description = "Smoke sample connections",
            ActualValue = sampleConnections.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

        if (string.Equals(transport.StreamType, "none", StringComparison.OrdinalIgnoreCase))
        {
            await RunHandshakeSmokeAsync(connectionOptions, sampleConnections, cancellationTokenSource.Token, observations);
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationTokenSource.Token);
            return;
        }

        observations.Add(new ValidationObservation
        {
            Category = "quic",
            Description = "Smoke sample streams per connection",
            ActualValue = sampleStreams.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

        if (string.Equals(transport.OpenPattern, "churn", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(transport.Behavior, "connection-churn", StringComparison.OrdinalIgnoreCase))
        {
            await RunChurnSmokeAsync(connectionOptions, sampleConnections, sampleStreams, payload, transport.StreamType, cancellationTokenSource.Token, observations);
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationTokenSource.Token);
            return;
        }

        await RunConnectionSmokeAsync(connectionOptions, sampleConnections, sampleStreams, payload, transport.StreamType, transport.OpenPattern, cancellationTokenSource.Token, observations);
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationTokenSource.Token);
    }

    private static async Task RunHandshakeSmokeAsync(
        QuicClientConnectionOptions connectionOptions,
        int sampleConnections,
        CancellationToken cancellationToken,
        List<ValidationObservation> observations)
    {
        for (var index = 0; index < sampleConnections; index++)
        {
            var connectStopwatch = System.Diagnostics.Stopwatch.StartNew();
            await using var connection = await QuicConnection.ConnectAsync(connectionOptions, cancellationToken);
            connectStopwatch.Stop();

            observations.Add(new ValidationObservation
            {
                Category = "quic",
                Description = $"Handshake smoke #{index + 1}",
                ActualValue = connectStopwatch.Elapsed.TotalMilliseconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            });
        }
    }

    private static async Task RunConnectionSmokeAsync(
        QuicClientConnectionOptions connectionOptions,
        int sampleConnections,
        int sampleStreams,
        byte[] payload,
        string streamType,
        string openPattern,
        CancellationToken cancellationToken,
        List<ValidationObservation> observations)
    {
        for (var connectionIndex = 0; connectionIndex < sampleConnections; connectionIndex++)
        {
            var connectStopwatch = System.Diagnostics.Stopwatch.StartNew();
            await using var connection = await QuicConnection.ConnectAsync(connectionOptions, cancellationToken);
            connectStopwatch.Stop();

            observations.Add(new ValidationObservation
            {
                Category = "quic",
                Description = $"Handshake smoke #{connectionIndex + 1}",
                ActualValue = connectStopwatch.Elapsed.TotalMilliseconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            });

            if (sampleStreams <= 0)
            {
                continue;
            }

            if (string.Equals(openPattern, "concurrent", StringComparison.OrdinalIgnoreCase))
            {
                var tasks = Enumerable.Range(0, sampleStreams)
                    .Select(streamIndex => RunStreamSmokeAsync(connection, payload, streamType, streamIndex + 1, cancellationToken, observations))
                    .ToArray();
                await Task.WhenAll(tasks);
            }
            else
            {
                for (var streamIndex = 0; streamIndex < sampleStreams; streamIndex++)
                {
                    await RunStreamSmokeAsync(connection, payload, streamType, streamIndex + 1, cancellationToken, observations);
                }
            }
        }
    }

    private static async Task RunChurnSmokeAsync(
        QuicClientConnectionOptions connectionOptions,
        int sampleConnections,
        int sampleStreams,
        byte[] payload,
        string streamType,
        CancellationToken cancellationToken,
        List<ValidationObservation> observations)
    {
        var totalIterations = Math.Max(sampleConnections * sampleStreams, 1);
        for (var iteration = 0; iteration < totalIterations; iteration++)
        {
            var connectStopwatch = System.Diagnostics.Stopwatch.StartNew();
            await using var connection = await QuicConnection.ConnectAsync(connectionOptions, cancellationToken);
            connectStopwatch.Stop();

            observations.Add(new ValidationObservation
            {
                Category = "quic",
                Description = $"Churn smoke handshake #{iteration + 1}",
                ActualValue = connectStopwatch.Elapsed.TotalMilliseconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            });

            if (!string.Equals(streamType, "none", StringComparison.OrdinalIgnoreCase))
            {
                await RunStreamSmokeAsync(connection, payload, streamType, iteration + 1, cancellationToken, observations);
            }
        }
    }

    private static async Task RunStreamSmokeAsync(
        QuicConnection connection,
        byte[] payload,
        string streamType,
        int sampleIndex,
        CancellationToken cancellationToken,
        List<ValidationObservation> observations)
    {
        var streamKind = string.Equals(streamType, "unidirectional", StringComparison.OrdinalIgnoreCase)
            ? QuicStreamType.Unidirectional
            : QuicStreamType.Bidirectional;

        await using var stream = await connection.OpenOutboundStreamAsync(streamKind, cancellationToken);

        var transactionStopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (payload.Length > 0)
        {
            await stream.WriteAsync(payload.AsMemory(), cancellationToken);
        }

        await stream.CompleteWritesAsync(cancellationToken);

        long bytesReceived = 0;
        long bytesSent = payload.Length;
        var firstByteElapsed = TimeSpan.Zero;

        if (streamKind == QuicStreamType.Bidirectional)
        {
            var firstRead = true;
            var buffer = new byte[Math.Max(1, Math.Min(payload.Length, 64 * 1024))];
            var received = new List<byte>(payload.Length == 0 ? 1 : payload.Length);

            while (true)
            {
                var readStopwatch = firstRead ? System.Diagnostics.Stopwatch.StartNew() : null;
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (firstRead)
                {
                    readStopwatch!.Stop();
                    firstByteElapsed = readStopwatch.Elapsed;
                    firstRead = false;
                }

                if (bytesRead <= 0)
                {
                    break;
                }

                received.AddRange(buffer.AsSpan(0, bytesRead).ToArray());
                bytesReceived += bytesRead;
            }

            if (payload.Length > 0 && !received.Take(payload.Length).SequenceEqual(payload))
            {
                throw new InvalidDataException("QUIC smoke stream echo did not match the sent payload.");
            }

            observations.Add(new ValidationObservation
            {
                Category = "quic",
                Description = $"Bidirectional stream smoke #{sampleIndex}",
                ActualValue = bytesReceived.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });

            observations.Add(new ValidationObservation
            {
                Category = "quic",
                Description = $"Bidirectional stream TTFB #{sampleIndex}",
                ActualValue = firstByteElapsed.TotalMilliseconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            });
        }
        else
        {
            observations.Add(new ValidationObservation
            {
                Category = "quic",
                Description = $"Unidirectional stream smoke #{sampleIndex}",
                ActualValue = bytesSent.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        }

        transactionStopwatch.Stop();
        observations.Add(new ValidationObservation
        {
            Category = "quic",
            Description = $"Stream transaction duration #{sampleIndex}",
            ActualValue = transactionStopwatch.Elapsed.TotalMilliseconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
        });
    }

    private static QuicClientConnectionOptions CreateConnectionOptions(Uri uri, string certificateMode)
    {
        return new QuicClientConnectionOptions
        {
            RemoteEndPoint = CreateRemoteEndPoint(uri),
            DefaultCloseErrorCode = 0,
            DefaultStreamErrorCode = 0,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                TargetHost = string.IsNullOrWhiteSpace(uri.Host) ? "localhost" : uri.Host,
                AllowRenegotiation = false,
                AllowTlsResume = false,
                AllowRsaPkcs1Padding = false,
                AllowRsaPssPadding = false,
                EnabledSslProtocols = SslProtocols.Tls13,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                ApplicationProtocols = [new SslApplicationProtocol("plab-raw-quic")],
                RemoteCertificateValidationCallback = static (_, _, _, _) => true
            }
        };
    }

    private static EndPoint CreateRemoteEndPoint(Uri uri)
    {
        if (IPAddress.TryParse(uri.Host, out var address))
        {
            return new IPEndPoint(address, uri.Port);
        }

        return new DnsEndPoint(uri.Host, uri.Port);
    }

    private static byte[] CreateDeterministicBytes(int size)
    {
        if (size <= 0)
        {
            return [];
        }

        var bytes = new byte[size];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index % 251);
        }

        return bytes;
    }
}
