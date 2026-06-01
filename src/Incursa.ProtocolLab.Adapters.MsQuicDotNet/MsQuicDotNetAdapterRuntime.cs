// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Model;
using Microsoft.AspNetCore.Http;

namespace Incursa.ProtocolLab.Adapters.MsQuicDotNet;

public sealed record MsQuicDotNetAdapterOptions
{
    public string ControlPlaneBaseUrl { get; init; } = "";

    public string AdapterIdentityId { get; init; } = "msquic-dotnet-raw-adapter-v1";

    public string AdapterIdentityName { get; init; } = "MSQuic/.NET Raw QUIC Adapter v1";

    public string AdapterIdentityVersion { get; init; } = "1.0.0";

    public string ImplementationId { get; init; } = "msquic-dotnet-raw";

    public string ImplementationName { get; init; } = "MSQuic/.NET Raw QUIC";

    public string ImplementationVersion { get; init; } = "1.0.0";

    public string ImplementationImage { get; init; } = "";

    public string ContractVersion { get; init; } = "v1";

    public string SupportedScenarioSelectorExpression { get; init; } =
        "fixture.quic.handshake|fixture.quic.bidirectional-echo|fixture.quic.bidirectional-bulk|quic.transport.handshake-cold|quic.transport.stream-throughput.1mb|quic.transport.connection-churn|quic.transport.multiplex.100x64kb|quic.transport.duplex-streams";

    public int QuicPort { get; init; }

    public string QuicAlpn { get; init; } = "plab-raw-quic";

    public string CertificateSubject { get; init; } = "CN=ProtocolLab-MSQuic-Local";

    public TimeSpan StartTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan ReadinessTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public string DefaultControlPlaneContentType { get; init; } = "application/json";

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}

public sealed class MsQuicDotNetAdapterRuntime
{
    private readonly MsQuicDotNetAdapterOptions options;
    private readonly ConcurrentDictionary<string, MsQuicDotNetSession> sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> deletedSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();
    private int sessionCounter;

    public bool IsQuicSupported => QuicListener.IsSupported;

    public MsQuicDotNetAdapterRuntime(MsQuicDotNetAdapterOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<AdapterHealthResponse> GetHealthAsync()
    {
        return Task.FromResult(new AdapterHealthResponse
        {
            AdapterIdentity = CreateAdapterIdentity(),
            Status = IsQuicSupported ? AdapterHealthStatus.Ready : AdapterHealthStatus.Degraded,
            VersionCompatibility = CreateCompatibility(),
            Message = IsQuicSupported
                ? "MSQuic/.NET raw QUIC adapter ready."
                : "MSQuic/.NET raw QUIC adapter degraded: QuicListener is not supported on this platform.",
            ObservedAt = Now(),
            Capabilities = CreateCapabilities()
        });
    }

    public Task<AdapterManifestResponse> GetManifestAsync()
    {
        var metricsAvailable = new AdapterMetricsAvailability
        {
            Available = true,
            SessionMetricsAvailable = true,
            EndpointMetricsAvailable = true,
            ProcessMetricsAvailable = false,
            ContainerMetricsAvailable = false,
            AvailableKinds = ["snapshot"]
        };

        return Task.FromResult(new AdapterManifestResponse
        {
            AdapterIdentity = CreateAdapterIdentity(),
            ImplementationIdentity = CreateImplementationIdentity(),
            VersionCompatibility = CreateCompatibility(),
            SupportedRoles = ["server"],
            ClaimedCapabilities = CreateCapabilities(),
            SupportedScenarioSelectors =
            [
                new AdapterScenarioSelector
                {
                    SelectorType = "scenario-id",
                    Expression = options.SupportedScenarioSelectorExpression,
                    Description = "Raw QUIC transport scenarios supported by the MSQuic/.NET adapter."
                }
            ],
            SupportedEndpointTypes =
            [
                new AdapterEndpointType
                {
                    Type = "quic",
                    Description = "Raw QUIC/UDP endpoint using System.Net.Quic.",
                    Protocols = ["quic"],
                    Extensions = new Dictionary<string, JsonElement>
                    {
                        ["transport"] = ProtocolLabAdapterJson.SerializeValue("udp"),
                        ["alpn"] = ProtocolLabAdapterJson.SerializeValue(options.QuicAlpn),
                        ["streamModel"] = ProtocolLabAdapterJson.SerializeValue("quic-stream"),
                        ["supportedStreamDirections"] = ProtocolLabAdapterJson.SerializeValue(new[] { "bidirectional" }),
                        ["datagramSupported"] = ProtocolLabAdapterJson.SerializeValue(false),
                        ["zeroRttSupported"] = ProtocolLabAdapterJson.SerializeValue(false)
                    }
                }
            ],
            SupportedArtifactTypes =
            [
                new AdapterArtifactType
                {
                    Type = "stdout",
                    Description = "MSQuic/.NET adapter stdout capture.",
                    ProducedByStates = ["starting", "running", "stopping", "stopped"]
                },
                new AdapterArtifactType
                {
                    Type = "stderr",
                    Description = "MSQuic/.NET adapter stderr capture.",
                    ProducedByStates = ["starting", "running", "stopping", "stopped"]
                },
                new AdapterArtifactType
                {
                    Type = "session",
                    Description = "Session state snapshot.",
                    ProducedByStates = ["created", "prepared", "running", "ready", "stopped", "disposed"]
                },
                new AdapterArtifactType
                {
                    Type = "endpoint",
                    Description = "Protocol endpoint snapshot.",
                    ProducedByStates = ["prepared", "running", "ready"]
                },
                new AdapterArtifactType
                {
                    Type = "server-log",
                    Description = "QUIC server runtime log.",
                    ProducedByStates = ["running", "ready", "stopped"]
                }
            ],
            MetricsAvailability = metricsAvailable,
            DefaultResponseContentTypes = [options.DefaultControlPlaneContentType]
        });
    }

    public Task<AdapterSessionResource> CreateSessionAsync(AdapterSessionCreateRequest request)
    {
        var sessionId = !string.IsNullOrWhiteSpace(request.RequestedSessionId)
            ? request.RequestedSessionId!
            : BuildSessionId();

        lock (gate)
        {
            deletedSessions.Remove(sessionId);
            var session = new MsQuicDotNetSession(sessionId, CreateSessionDirectory(request.RunId, request.CellId, sessionId))
            {
                Summary = new AdapterSessionSummary
                {
                    SessionId = sessionId,
                    State = AdapterSessionState.Created,
                    RunId = request.RunId,
                    CellId = request.CellId,
                    CreatedAt = Now(),
                    UpdatedAt = Now()
                }
            };

            sessions[sessionId] = session;
            session.WriteSessionSnapshot();

            return Task.FromResult(new AdapterSessionResource
            {
                Session = session.Summary,
                Operation = new AdapterOperationResult
                {
                    Category = AdapterOperationResultCategory.Succeeded,
                    Message = "Session created."
                }
            });
        }
    }

    public Task<AdapterSessionResource> GetSessionAsync(string sessionId)
    {
        var session = GetSession(sessionId);
        return Task.FromResult(new AdapterSessionResource
        {
            Session = session.Summary,
            Operation = session.LastOperation
        });
    }

    public Task<AdapterOperationResult> PrepareAsync(string sessionId, AdapterPrepareRequest request)
    {
        var session = GetSession(sessionId);
        lock (session.Gate)
        {
            if (session.Summary.State is AdapterSessionState.Preparing or AdapterSessionState.Starting or AdapterSessionState.Running or AdapterSessionState.Ready)
            {
                throw CreateProblem(
                    "invalid-transition",
                    StatusCodes.Status409Conflict,
                    "Session is already prepared or running.",
                    operation: "prepare",
                    sessionId: sessionId);
            }

            if (!IsQuicSupported)
            {
                session.Summary = session.Summary with
                {
                    State = AdapterSessionState.Unsupported
                };
                session.LastOperation = new AdapterOperationResult
                {
                    Category = AdapterOperationResultCategory.Unsupported,
                    Message = "System.Net.Quic is not supported on this platform.",
                    Code = "unsupported-platform",
                    Warnings = []
                };
                session.WriteSessionSnapshot();
                return Task.FromResult(session.LastOperation);
            }

            var scenario = NormalizeScenario(request, DeserializeScenario(request.ScenarioDocument));
            var requestedProtocol = ResolveRequestedProtocol(request, scenario);
            var support = EvaluateSupport(scenario, requestedProtocol);
            session.Scenario = scenario;
            session.RequestedProtocol = requestedProtocol;
            session.RequestedEndpointBindings = request.RequestedEndpointBindings;
            session.ArtifactOutputExpectations = request.ArtifactOutputExpectations;
            session.Extensions = request.Extensions;
            session.Summary = session.Summary with
            {
                ScenarioId = request.ScenarioId,
                ScenarioVersion = request.ScenarioVersion,
                Role = request.Role,
                RunId = request.RunId,
                CellId = request.CellId,
                UpdatedAt = Now(),
                Warnings = support.Warnings
            };

            if (!support.IsSupported)
            {
                session.Summary = session.Summary with
                {
                    State = AdapterSessionState.Unsupported
                };
                session.LastOperation = new AdapterOperationResult
                {
                    Category = AdapterOperationResultCategory.Unsupported,
                    Message = support.Reason,
                    Code = "unsupported",
                    Warnings = support.Warnings
                };
                session.WriteSessionSnapshot();
                return Task.FromResult(session.LastOperation);
            }

            session.EndpointPlan = new MsQuicDotNetEndpointPlan(
                options.QuicAlpn,
                options.CertificateSubject,
                scenario);
            session.Summary = session.Summary with
            {
                State = AdapterSessionState.Prepared
            };
            session.LastOperation = new AdapterOperationResult
            {
                Category = AdapterOperationResultCategory.Succeeded,
                Message = "Session prepared."
            };
            session.WriteSessionSnapshot();
            return Task.FromResult(session.LastOperation);
        }
    }

    public async Task<AdapterOperationResult> StartAsync(string sessionId)
    {
        var session = GetSession(sessionId);
        lock (session.Gate)
        {
            if (session.Summary.State == AdapterSessionState.Created)
            {
                throw CreateProblem(
                    "invalid-transition",
                    StatusCodes.Status409Conflict,
                    "Session must be prepared before start.",
                    operation: "start",
                    sessionId: sessionId);
            }

            if (session.Summary.State == AdapterSessionState.Unsupported)
            {
                session.LastOperation ??= new AdapterOperationResult
                {
                    Category = AdapterOperationResultCategory.Unsupported,
                    Message = "Session was marked unsupported during prepare.",
                    Code = "unsupported"
                };
                return session.LastOperation;
            }

            if (session.QuicServer is not null)
            {
                return session.LastOperation ?? new AdapterOperationResult
                {
                    Category = AdapterOperationResultCategory.Succeeded,
                    Message = "Session already started."
                };
            }

            if (session.EndpointPlan is null)
            {
                throw CreateProblem(
                    "invalid-transition",
                    StatusCodes.Status409Conflict,
                    "Session must be prepared before start.",
                    operation: "start",
                    sessionId: sessionId);
            }

            session.Summary = session.Summary with
            {
                State = AdapterSessionState.Starting,
                UpdatedAt = Now()
            };
            session.WriteSessionSnapshot();
        }

        MsQuicDotNetQuicServer quicServer;
        try
        {
            quicServer = await MsQuicDotNetQuicServer.StartAsync(
                session,
                session.EndpointPlan!,
                options);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException or System.Security.Authentication.AuthenticationException)
        {
            lock (session.Gate)
            {
                session.Summary = session.Summary with
                {
                    State = AdapterSessionState.Failed,
                    UpdatedAt = Now()
                };
                session.LastOperation = new AdapterOperationResult
                {
                    Category = AdapterOperationResultCategory.Failed,
                    Message = exception.Message,
                    Code = "start-failed"
                };
                session.WriteSessionSnapshot();
                return session.LastOperation;
            }
        }

        lock (session.Gate)
        {
            session.QuicServer = quicServer;
            session.Endpoint = quicServer.Endpoint;
            session.WriteEndpointSnapshot();
        }

        var readiness = await WaitForReadinessAsync(session, quicServer);
        MsQuicDotNetQuicServer? serverToStop = null;
        lock (session.Gate)
        {
            if (readiness.IsReady)
            {
                session.Summary = session.Summary with
                {
                    State = AdapterSessionState.Ready,
                    UpdatedAt = Now()
                };
                session.LastOperation = new AdapterOperationResult
                {
                    Category = AdapterOperationResultCategory.Succeeded,
                    Message = "Session started."
                };
            }
            else
            {
                serverToStop = session.QuicServer;
                session.QuicServer = null;

                session.Summary = session.Summary with
                {
                    State = readiness.IsUnsupported ? AdapterSessionState.Unsupported : AdapterSessionState.Failed,
                    UpdatedAt = Now()
                };
                session.LastOperation = new AdapterOperationResult
                {
                    Category = readiness.IsUnsupported ? AdapterOperationResultCategory.Unsupported : AdapterOperationResultCategory.Failed,
                    Message = readiness.Message,
                    Code = readiness.IsUnsupported ? "unsupported" : "readiness-failed",
                    Warnings = readiness.Warnings
                };
            }

            session.WriteSessionSnapshot();
        }

        if (serverToStop is not null)
        {
            await serverToStop.StopAsync();
        }

        lock (session.Gate)
        {
            session.WriteSessionSnapshot();
            return session.LastOperation!;
        }
    }

    public Task<AdapterStatusResponse> GetStatusAsync(string sessionId)
    {
        var session = GetSession(sessionId);
        lock (session.Gate)
        {
            var readiness = session.Summary.State switch
            {
                AdapterSessionState.Ready => new AdapterReadinessSnapshot
                {
                    Status = AdapterReadinessStatus.Ready,
                    Message = "MSQuic/.NET raw QUIC endpoint ready.",
                    ObservedAt = Now(),
                    Warnings = session.Summary.Warnings
                },
                AdapterSessionState.Unsupported => new AdapterReadinessSnapshot
                {
                    Status = AdapterReadinessStatus.Unsupported,
                    Message = session.LastOperation?.Message ?? "Unsupported scenario.",
                    ObservedAt = Now(),
                    Warnings = session.Summary.Warnings
                },
                AdapterSessionState.Failed => new AdapterReadinessSnapshot
                {
                    Status = AdapterReadinessStatus.Failed,
                    Message = session.LastOperation?.Message ?? "Endpoint not ready.",
                    ObservedAt = Now(),
                    Warnings = session.Summary.Warnings
                },
                AdapterSessionState.Running or AdapterSessionState.Starting or AdapterSessionState.Prepared => new AdapterReadinessSnapshot
                {
                    Status = AdapterReadinessStatus.NotReady,
                    Message = "Endpoint is not ready yet.",
                    ObservedAt = Now(),
                    Warnings = session.Summary.Warnings
                },
                _ => new AdapterReadinessSnapshot
                {
                    Status = AdapterReadinessStatus.Unknown,
                    Message = "Session has not been started.",
                    ObservedAt = Now(),
                    Warnings = session.Summary.Warnings
                }
            };

            return Task.FromResult(new AdapterStatusResponse
            {
                Session = session.Summary,
                Readiness = readiness,
                Health = new AdapterHealthSnapshot
                {
                    Status = IsQuicSupported ? AdapterHealthStatus.Ready : AdapterHealthStatus.Degraded,
                    Message = "MSQuic/.NET raw QUIC adapter ready.",
                    ObservedAt = Now()
                },
                Operation = session.LastOperation
            });
        }
    }

    public Task<AdapterEndpointsResponse> GetEndpointsAsync(string sessionId)
    {
        var session = GetSession(sessionId);
        lock (session.Gate)
        {
            IReadOnlyList<AdapterEndpoint> endpoints = session.Endpoint is null
                ? Array.Empty<AdapterEndpoint>()
                : [session.Endpoint];

            return Task.FromResult(new AdapterEndpointsResponse
            {
                Session = session.Summary,
                Endpoints = endpoints,
                Operation = new AdapterOperationResult
                {
                    Category = session.Summary.State == AdapterSessionState.Unsupported ? AdapterOperationResultCategory.Unsupported : AdapterOperationResultCategory.Succeeded,
                    Message = session.Endpoint is null ? "No endpoint available." : "Protocol endpoint discovered.",
                    Warnings = session.Summary.Warnings
                }
            });
        }
    }

    public Task<AdapterMetricsResponse> GetMetricsAsync(string sessionId)
    {
        var session = GetSession(sessionId);
        lock (session.Gate)
        {
            var metrics = new List<AdapterMetric>();
            if (session.QuicServer is not null)
            {
                metrics.AddRange(CreateQuicMetrics(session));
            }

            return Task.FromResult(new AdapterMetricsResponse
            {
                Session = session.Summary,
                Availability = session.Summary.State == AdapterSessionState.Unsupported
                    ? AdapterResourceAvailability.Unsupported
                    : AdapterResourceAvailability.Available,
                CapturedAt = Now(),
                Metrics = metrics,
                Notes = session.Summary.State == AdapterSessionState.Unsupported
                    ? ["Session was marked unsupported."]
                    : [],
                Operation = new AdapterOperationResult
                {
                    Category = session.Summary.State == AdapterSessionState.Unsupported ? AdapterOperationResultCategory.Unsupported : AdapterOperationResultCategory.Succeeded,
                    Message = session.Summary.State == AdapterSessionState.Unsupported ? "Metrics unavailable for unsupported session." : "Metrics snapshot captured."
                }
            });
        }
    }

    public Task<AdapterArtifactsResponse> GetArtifactsAsync(string sessionId)
    {
        var session = GetSession(sessionId);
        lock (session.Gate)
        {
            return Task.FromResult(new AdapterArtifactsResponse
            {
                Session = session.Summary,
                Availability = session.Summary.State == AdapterSessionState.Unsupported
                    ? AdapterResourceAvailability.Unsupported
                    : AdapterResourceAvailability.Available,
                Artifacts = BuildArtifacts(session),
                Operation = new AdapterOperationResult
                {
                    Category = session.Summary.State == AdapterSessionState.Unsupported ? AdapterOperationResultCategory.Unsupported : AdapterOperationResultCategory.Succeeded,
                    Message = "Artifacts discovered."
                }
            });
        }
    }

    public async Task<AdapterOperationResult> StopAsync(string sessionId)
    {
        var session = GetSession(sessionId);
        var originalState = session.Summary.State;
        MsQuicDotNetQuicServer? quicServer;
        lock (session.Gate)
        {
            quicServer = session.QuicServer;
            session.Summary = session.Summary with
            {
                State = AdapterSessionState.Stopping,
                UpdatedAt = Now()
            };
            session.WriteSessionSnapshot();
        }

        if (quicServer is not null)
        {
            await quicServer.StopAsync();
        }

        lock (session.Gate)
        {
            session.Summary = session.Summary with
            {
                State = originalState == AdapterSessionState.Unsupported ? AdapterSessionState.Unsupported : AdapterSessionState.Stopped,
                UpdatedAt = Now()
            };
            session.LastOperation = new AdapterOperationResult
            {
                Category = originalState == AdapterSessionState.Unsupported ? AdapterOperationResultCategory.Unsupported : AdapterOperationResultCategory.Succeeded,
                Message = "Session stopped."
            };
            session.WriteSessionSnapshot();
            return session.LastOperation;
        }
    }

    public async Task<AdapterSessionResource> DeleteSessionAsync(string sessionId)
    {
        var session = GetOrCreateDeletedSession(sessionId);
        await StopIfNeededAsync(session);

        lock (gate)
        {
            deletedSessions.Add(sessionId);
            sessions.TryRemove(sessionId, out _);
        }

        lock (session.Gate)
        {
            session.Summary = session.Summary with
            {
                State = AdapterSessionState.Disposed,
                UpdatedAt = Now()
            };
            session.LastOperation = new AdapterOperationResult
            {
                Category = AdapterOperationResultCategory.Succeeded,
                Message = "Session deleted."
            };
            session.WriteSessionSnapshot();
            return new AdapterSessionResource
            {
                Session = session.Summary,
                Operation = session.LastOperation
            };
        }
    }

    private async Task StopIfNeededAsync(MsQuicDotNetSession session)
    {
        MsQuicDotNetQuicServer? quicServer;
        lock (session.Gate)
        {
            quicServer = session.QuicServer;
            session.QuicServer = null;
        }

        if (quicServer is not null)
        {
            await quicServer.StopAsync();
        }
    }

    private MsQuicDotNetSession GetOrCreateDeletedSession(string sessionId)
    {
        if (sessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        lock (gate)
        {
            if (sessions.TryGetValue(sessionId, out session))
            {
                return session;
            }

            if (deletedSessions.Contains(sessionId))
            {
                return new MsQuicDotNetSession(sessionId, CreateSessionDirectory(null, null, sessionId))
                {
                    Summary = new AdapterSessionSummary
                    {
                        SessionId = sessionId,
                        State = AdapterSessionState.Disposed,
                        UpdatedAt = Now()
                    }
                };
            }

            throw CreateProblem(
                "session-not-found",
                StatusCodes.Status404NotFound,
                "Unknown session.",
                operation: "session",
                sessionId: sessionId);
        }
    }

    private MsQuicDotNetSession GetSession(string sessionId)
    {
        if (sessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        lock (gate)
        {
            if (sessions.TryGetValue(sessionId, out session))
            {
                return session;
            }

            if (deletedSessions.Contains(sessionId))
            {
                throw CreateProblem(
                    "session-not-found",
                    StatusCodes.Status404NotFound,
                    "Unknown session.",
                    operation: "session",
                    sessionId: sessionId);
            }
        }

        throw CreateProblem(
            "session-not-found",
            StatusCodes.Status404NotFound,
            "Unknown session.",
            operation: "session",
            sessionId: sessionId);
    }

    private AdapterIdentity CreateAdapterIdentity()
    {
        return new AdapterIdentity
        {
            Id = options.AdapterIdentityId,
            Name = options.AdapterIdentityName,
            Version = options.AdapterIdentityVersion,
            Vendor = "Incursa"
        };
    }

    private AdapterIdentity CreateImplementationIdentity()
    {
        return new AdapterIdentity
        {
            Id = options.ImplementationId,
            Name = options.ImplementationName,
            Version = options.ImplementationVersion,
            Image = options.ImplementationImage
        };
    }

    private AdapterVersionCompatibility CreateCompatibility()
    {
        return new AdapterVersionCompatibility
        {
            ContractVersion = options.ContractVersion,
            CompatibleContractVersions = [options.ContractVersion]
        };
    }

    private IReadOnlyList<AdapterCapability> CreateCapabilities()
    {
        var quicStatus = IsQuicSupported
            ? AdapterCapabilityStatus.Supported
            : AdapterCapabilityStatus.Unsupported;

        return
        [
            new AdapterCapability
            {
                Id = "adapter-control-plane",
                Status = AdapterCapabilityStatus.Supported,
                Description = "ProtocolLab Adapter Contract v1 control plane."
            },
            new AdapterCapability
            {
                Id = "quic.server",
                Status = quicStatus,
                Description = IsQuicSupported
                    ? "Raw QUIC server endpoint using System.Net.Quic with MSQuic backend."
                    : "MSQuic/System.Net.Quic is not available on this platform."
            },
            new AdapterCapability
            {
                Id = "quicTransport",
                Status = quicStatus,
                Description = "Raw QUIC transport support."
            },
            new AdapterCapability
            {
                Id = "quicHandshake",
                Status = quicStatus,
                Description = "QUIC connection handshake support."
            },
            new AdapterCapability
            {
                Id = "quicStreams",
                Status = quicStatus,
                Description = "QUIC bidirectional stream support."
            },
            new AdapterCapability
            {
                Id = "quicMultiplexing",
                Status = quicStatus,
                Description = "QUIC multiplexed stream fan-out support."
            },
            new AdapterCapability
            {
                Id = "quicDuplex",
                Status = quicStatus,
                Description = "QUIC bidirectional stream payload support."
            }
        ];
    }

    private MsQuicDotNetScenarioSupport EvaluateSupport(ScenarioDefinition scenario, string requestedProtocol)
    {
        var warnings = new List<string>();

        var normalizedRequested = NormalizeProtocolInternal(requestedProtocol);
        if (normalizedRequested is null)
        {
            return MsQuicDotNetScenarioSupport.Unsupported($"The requested protocol '{requestedProtocol}' is not supported by the MSQuic/.NET adapter.", warnings);
        }

        if (!string.Equals(scenario.ImplementationRole, "server", StringComparison.OrdinalIgnoreCase))
        {
            return MsQuicDotNetScenarioSupport.Unsupported("The MSQuic/.NET adapter only supports server scenarios.", warnings);
        }

        if (!string.Equals(scenario.Family, "fixture.quic", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(scenario.Family, "quic.transport", StringComparison.OrdinalIgnoreCase))
        {
            return MsQuicDotNetScenarioSupport.Unsupported("The MSQuic/.NET adapter only supports 'fixture.quic' and 'quic.transport' family scenarios.", warnings);
        }

        if (!IsSupportedScenario(scenario.Id))
        {
            return MsQuicDotNetScenarioSupport.Unsupported($"The scenario '{scenario.Id}' is not supported by the MSQuic/.NET adapter.", warnings);
        }

        foreach (var capability in scenario.RequiredCapabilities)
        {
            if (!SupportsCapability(capability))
            {
                return MsQuicDotNetScenarioSupport.Unsupported($"The required capability '{capability}' is not supported.", warnings);
            }
        }

        return MsQuicDotNetScenarioSupport.Supported(warnings);
    }

    private static bool SupportsCapability(string capability)
    {
        return SupportedCapabilities.Contains(capability);
    }

    private static bool IsSupportedScenario(string scenarioId)
    {
        return SupportedScenarios.Contains(scenarioId);
    }

    private MsQuicDotNetEndpointPlan BuildEndpointPlan(MsQuicDotNetSession session, string requestedProtocol)
    {
        var protocol = NormalizeProtocolInternal(requestedProtocol);
        if (protocol is null)
        {
            throw CreateProblem(
                "unsupported",
                StatusCodes.Status422UnprocessableEntity,
                $"The MSQuic/.NET adapter only supports the 'quic' protocol, but '{requestedProtocol}' was requested.",
                operation: "prepare",
                sessionId: session.SessionId);
        }

        return new MsQuicDotNetEndpointPlan(
            options.QuicAlpn,
            options.CertificateSubject,
            session.Scenario!);
    }

    private static string? NormalizeProtocolInternal(string? requestedProtocol)
    {
        if (string.Equals(requestedProtocol, "quic", StringComparison.OrdinalIgnoreCase))
        {
            return "quic";
        }

        return null;
    }

    private async Task<MsQuicDotNetReadinessResult> WaitForReadinessAsync(MsQuicDotNetSession session, MsQuicDotNetQuicServer server)
    {
        var deadline = Now() + options.ReadinessTimeout;
        var errors = new List<string>();

        while (Now() < deadline)
        {
            if (server.HasFailed)
            {
                return MsQuicDotNetReadinessResult.Failed($"The QUIC server failed before readiness: {server.FailureReason ?? "unknown"}", errors);
            }

            if (server.IsListening)
            {
                return MsQuicDotNetReadinessResult.Ready(errors);
            }

            await Task.Delay(250);
        }

        var message = errors.Count == 0
            ? $"The QUIC server did not become ready within {options.ReadinessTimeout.TotalSeconds:0} seconds."
            : $"The QUIC server did not become ready within {options.ReadinessTimeout.TotalSeconds:0} seconds. Last error: {errors[^1]}";
        return MsQuicDotNetReadinessResult.Failed(message, errors);
    }

    private IReadOnlyList<AdapterArtifact> BuildArtifacts(MsQuicDotNetSession session)
    {
        var artifacts = new List<AdapterArtifact>
        {
            new()
            {
                ArtifactId = "server.stdout",
                ArtifactType = "stdout",
                Status = AdapterResourceAvailability.Available,
                Path = session.StdoutPath,
                ContentType = "text/plain",
                Final = true
            },
            new()
            {
                ArtifactId = "server.stderr",
                ArtifactType = "stderr",
                Status = AdapterResourceAvailability.Available,
                Path = session.StderrPath,
                ContentType = "text/plain",
                Final = true
            },
            new()
            {
                ArtifactId = "session.snapshot",
                ArtifactType = "session",
                Status = AdapterResourceAvailability.Available,
                Path = session.SessionSnapshotPath,
                ContentType = "application/json",
                Final = true
            }
        };

        if (!string.IsNullOrWhiteSpace(session.EndpointSnapshotPath))
        {
            artifacts.Add(new AdapterArtifact
            {
                ArtifactId = "endpoint.snapshot",
                ArtifactType = "endpoint",
                Status = AdapterResourceAvailability.Available,
                Path = session.EndpointSnapshotPath,
                ContentType = "application/json",
                Final = true
            });
        }

        if (!string.IsNullOrWhiteSpace(session.ServerLogPath))
        {
            artifacts.Add(new AdapterArtifact
            {
                ArtifactId = "server.log",
                ArtifactType = "server-log",
                Status = File.Exists(session.ServerLogPath)
                    ? AdapterResourceAvailability.Available
                    : AdapterResourceAvailability.Unavailable,
                Path = session.ServerLogPath,
                ContentType = "text/plain",
                Final = true
            });
        }

        return artifacts;
    }

    private IReadOnlyList<AdapterMetric> CreateQuicMetrics(MsQuicDotNetSession session)
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var metrics = new List<AdapterMetric>
        {
            new()
            {
                MetricId = "quic.listening",
                Scope = "endpoint",
                Value = JsonSerializer.SerializeToElement(session.QuicServer?.IsListening == true, jsonOptions),
                Notes = "Whether the QUIC server is currently listening."
            }
        };

        if (session.QuicServer is not null)
        {
            var serverMetrics = session.QuicServer.GetMetrics();
            metrics.AddRange(serverMetrics.Select(m => new AdapterMetric
            {
                MetricId = m.MetricId,
                Scope = m.Scope,
                Value = JsonSerializer.SerializeToElement(m.Value, jsonOptions),
                Notes = m.Notes
            }));
        }

        metrics.Add(new AdapterMetric
        {
            MetricId = "endpoint.port",
            Scope = "endpoint",
            Value = JsonSerializer.SerializeToElement(session.Endpoint?.Port ?? 0, jsonOptions),
            Notes = "QUIC protocol endpoint port."
        });

        return metrics;
    }

    private ScenarioDefinition DeserializeScenario(JsonElement scenarioDocument)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return JsonSerializer.Deserialize<ScenarioDefinition>(scenarioDocument.GetRawText(), options)
            ?? throw CreateProblem(
                "invalid-scenario",
                StatusCodes.Status400BadRequest,
                "The scenario document could not be parsed.",
                operation: "prepare");
    }

    private static ScenarioDefinition NormalizeScenario(AdapterPrepareRequest request, ScenarioDefinition scenario)
    {
        if (!string.IsNullOrWhiteSpace(scenario.Id))
        {
            return scenario;
        }

        return new ScenarioDefinition
        {
            Id = request.ScenarioId,
            Name = request.ScenarioId,
            Version = request.ScenarioVersion,
            Description = request.ScenarioId,
            Protocol = scenario.Protocol,
            ImplementationRole = request.Role,
            RequiredCapabilities = scenario.RequiredCapabilities,
            Endpoint = scenario.Endpoint,
            H3Protocol = scenario.H3Protocol,
            QuicTransport = scenario.QuicTransport,
            WebTransport = scenario.WebTransport,
            Masque = scenario.Masque,
            Validation = scenario.Validation,
            Benchmark = scenario.Benchmark,
            NetworkProfile = scenario.NetworkProfile,
            RequiredMetrics = scenario.RequiredMetrics,
            ArtifactRequirements = scenario.ArtifactRequirements,
            Tags = scenario.Tags
        };
    }

    private string ResolveRequestedProtocol(AdapterPrepareRequest request, ScenarioDefinition scenario)
    {
        var bindingProtocol = request.RequestedEndpointBindings.FirstOrDefault()?.EndpointType;
        if (!string.IsNullOrWhiteSpace(bindingProtocol))
        {
            return bindingProtocol!;
        }

        return scenario.Protocol;
    }

    private string CreateSessionDirectory(string? runId, string? cellId, string sessionId)
    {
        var segments = new List<string> { Directory.GetCurrentDirectory(), ".artifacts", "runs" };
        if (!string.IsNullOrWhiteSpace(runId))
        {
            segments.Add(ArtifactLayout.SanitizeSegment(runId));
        }

        if (!string.IsNullOrWhiteSpace(cellId))
        {
            segments.Add(ArtifactLayout.SanitizeSegment(cellId));
        }

        segments.Add("adapter");
        segments.Add(ArtifactLayout.SanitizeSegment(sessionId));
        var directory = Path.Combine(segments.ToArray());
        Directory.CreateDirectory(directory);
        return directory;
    }

    private string BuildSessionId()
    {
        lock (gate)
        {
            sessionCounter++;
            return $"msquic-raw-adapter-{sessionCounter:0000}";
        }
    }

    private DateTimeOffset Now()
    {
        return options.TimeProvider.GetUtcNow();
    }

    private static MsQuicDotNetAdapterProblemException CreateProblem(string code, int status, string title, string operation, string? sessionId = null)
    {
        return new MsQuicDotNetAdapterProblemException(new AdapterProblemDetails
        {
            Type = "https://incursa.example/problems/msquic-dotnet-adapter",
            Title = title,
            Status = status,
            Code = code,
            Operation = operation,
            SessionId = sessionId,
            Retryable = false
        }, (HttpStatusCode)status);
    }

    private static readonly HashSet<string> SupportedScenarios = new(StringComparer.OrdinalIgnoreCase)
    {
        "fixture.quic.handshake",
        "fixture.quic.bidirectional-echo",
        "fixture.quic.bidirectional-bulk",
        "quic.transport.handshake-cold",
        "quic.transport.stream-throughput.1mb",
        "quic.transport.connection-churn",
        "quic.transport.multiplex.100x64kb",
        "quic.transport.duplex-streams"
    };

    private static readonly HashSet<string> SupportedCapabilities = new(StringComparer.OrdinalIgnoreCase)
    {
        "quicTransport",
        "quicHandshake",
        "quicStreams",
        "quicMultiplexing",
        "quicDuplex"
    };
}

public sealed class MsQuicDotNetAdapterProblemException : Exception
{
    public MsQuicDotNetAdapterProblemException(AdapterProblemDetails problem, HttpStatusCode statusCode)
        : base(problem.Title)
    {
        Problem = problem;
        StatusCode = statusCode;
    }

    public AdapterProblemDetails Problem { get; }

    public HttpStatusCode StatusCode { get; }
}

internal sealed class MsQuicDotNetSession
{
    public MsQuicDotNetSession(string sessionId, string sessionDirectory)
    {
        SessionId = sessionId;
        SessionDirectory = sessionDirectory;
        SessionSnapshotPath = Path.Combine(SessionDirectory, "session.json");
        EndpointSnapshotPath = Path.Combine(SessionDirectory, "endpoint.json");
        StdoutPath = Path.Combine(SessionDirectory, "server.stdout.txt");
        StderrPath = Path.Combine(SessionDirectory, "server.stderr.txt");
        CommandLinePath = Path.Combine(SessionDirectory, "server.command.txt");
        ServerLogPath = Path.Combine(SessionDirectory, "server.log.txt");
        Directory.CreateDirectory(SessionDirectory);
    }

    public string SessionId { get; }

    public string SessionDirectory { get; }

    public string SessionSnapshotPath { get; }

    public string EndpointSnapshotPath { get; }

    public string StdoutPath { get; }

    public string StderrPath { get; }

    public string CommandLinePath { get; }

    public string ServerLogPath { get; }

    public object Gate { get; } = new();

    public AdapterSessionSummary Summary { get; set; } = new();

    public AdapterOperationResult? LastOperation { get; set; }

    public ScenarioDefinition? Scenario { get; set; }

    public string? RequestedProtocol { get; set; }

    public IReadOnlyList<AdapterEndpointBinding> RequestedEndpointBindings { get; set; } = [];

    public IReadOnlyList<AdapterArtifactExpectation> ArtifactOutputExpectations { get; set; } = [];

    public Dictionary<string, JsonElement> Extensions { get; set; } = [];

    public MsQuicDotNetEndpointPlan? EndpointPlan { get; set; }

    public AdapterEndpoint? Endpoint { get; set; }

    public MsQuicDotNetQuicServer? QuicServer { get; set; }

    public void WriteSessionSnapshot()
    {
        File.WriteAllText(SessionSnapshotPath, JsonSerializer.Serialize(Summary, ProtocolLabAdapterJson.Options));
    }

    public void WriteEndpointSnapshot()
    {
        if (Endpoint is null)
        {
            return;
        }

        File.WriteAllText(EndpointSnapshotPath, JsonSerializer.Serialize(Endpoint, ProtocolLabAdapterJson.Options));
        File.WriteAllText(CommandLinePath, "in-process System.Net.Quic server");
    }
}

internal sealed record MsQuicDotNetEndpointPlan(
    string Alpn,
    string CertificateSubject,
    ScenarioDefinition Scenario);

internal sealed record MsQuicDotNetReadinessResult(bool IsReady, bool IsUnsupported, string Message, IReadOnlyList<string> Warnings)
{
    public static MsQuicDotNetReadinessResult Ready(IReadOnlyList<string> warnings) => new(true, false, "MSQuic/.NET QUIC server is ready.", warnings);

    public static MsQuicDotNetReadinessResult Failed(string message, IReadOnlyList<string> warnings) => new(false, false, message, warnings);

    public static MsQuicDotNetReadinessResult Unsupported(string message, IReadOnlyList<string> warnings) => new(false, true, message, warnings);
}

internal sealed record MsQuicDotNetScenarioSupport(bool IsSupported, string? Reason, IReadOnlyList<string> Warnings)
{
    public static MsQuicDotNetScenarioSupport Supported(IReadOnlyList<string> warnings) => new(true, null, warnings);

    public static MsQuicDotNetScenarioSupport Unsupported(string reason, IReadOnlyList<string> warnings) => new(false, reason, warnings);
}
