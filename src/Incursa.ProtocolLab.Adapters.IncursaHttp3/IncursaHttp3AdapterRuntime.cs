// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Model;
using Microsoft.AspNetCore.Http;

namespace Incursa.ProtocolLab.Adapters.IncursaHttp3;

public sealed record IncursaHttp3AdapterOptions
{
    public string RepositoryRoot { get; init; } = Directory.GetCurrentDirectory();

    public string ControlPlaneBaseUrl { get; init; } = "";

    public string AdapterIdentityId { get; init; } = "incursa-http3-adapter-v1";

    public string AdapterIdentityName { get; init; } = "Incursa HTTP/3 Adapter v1";

    public string AdapterIdentityVersion { get; init; } = "1.0.0";

    public string ImplementationId { get; init; } = "incursa-http3";

    public string ImplementationName { get; init; } = "Incursa HTTP/3";

    public string ImplementationVersion { get; init; } = "1.0.0";

    public string ImplementationImage { get; init; } = "incursa/protocol-lab-incursa-http3-bench-server:local";

    public string ContractVersion { get; init; } = "v1";

    public string SupportedScenarioSelectorExpression { get; init; } =
        "http.core.*|http.payload.*|http.upload.*|http.headers.*|fixture.incursa-http3.*";

    public string SupportedEndpointPathExpression { get; init; } =
        "/plaintext|/json|/status|/bytes/*|/stream/bytes|/headers/response|/inspect/headers|/echo|/hash|/sink|/upload";

    public TimeSpan StartTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan ReadinessTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public string DefaultControlPlaneContentType { get; init; } = "application/json";

    public string ReadinessProbePath { get; init; } = "/plaintext";

    public string? ForceEndpointStartFailureMessage { get; init; }

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}

public sealed class IncursaHttp3AdapterRuntime
{
    private readonly IncursaHttp3AdapterOptions options;
    private readonly ConcurrentDictionary<string, IncursaHttp3Session> sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> deletedSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();
    private int sessionCounter;

    public IncursaHttp3AdapterRuntime(IncursaHttp3AdapterOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<AdapterHealthResponse> GetHealthAsync()
    {
        return Task.FromResult(new AdapterHealthResponse
        {
            AdapterIdentity = CreateAdapterIdentity(),
            Status = AdapterHealthStatus.Ready,
            VersionCompatibility = CreateCompatibility(),
            Message = "Incursa HTTP/3 adapter ready.",
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
                    Description = "HTTP/3 application scenarios supported by the Incursa HTTP/3 adapter."
                },
                new AdapterScenarioSelector
                {
                    SelectorType = "endpoint-path",
                    Expression = options.SupportedEndpointPathExpression,
                    Description = "Supported benchmark endpoint paths."
                }
            ],
            SupportedEndpointTypes =
            [
                new AdapterEndpointType
                {
                    Type = "https",
                    Description = "Loopback HTTPS endpoint for adapter-backed Incursa HTTP/3 sessions.",
                    Protocols = ["h3"]
                }
            ],
            SupportedArtifactTypes =
            [
                new AdapterArtifactType
                {
                    Type = "stdout",
                    Description = "Session stdout capture for the adapter-backed Incursa HTTP/3 endpoint.",
                    ProducedByStates = ["created", "prepared", "starting", "running", "stopping", "stopped", "disposed"]
                },
                new AdapterArtifactType
                {
                    Type = "stderr",
                    Description = "Session stderr capture for the adapter-backed Incursa HTTP/3 endpoint.",
                    ProducedByStates = ["created", "prepared", "starting", "running", "stopping", "stopped", "disposed"]
                },
                new AdapterArtifactType
                {
                    Type = "session",
                    Description = "Session state snapshot.",
                    ProducedByStates = ["created", "prepared", "starting", "running", "ready", "stopping", "stopped", "failed", "unsupported", "disposed"]
                },
                new AdapterArtifactType
                {
                    Type = "endpoint",
                    Description = "Protocol endpoint snapshot.",
                    ProducedByStates = ["prepared", "starting", "running", "ready", "stopping", "stopped"]
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
            var session = new IncursaHttp3Session(sessionId, CreateSessionDirectory(request.RunId, request.CellId, sessionId))
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
            session.AppendStdout($"Session '{sessionId}' created.");

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
                session.AppendStderr(support.Reason ?? "Unsupported scenario.");
                session.WriteSessionSnapshot();
                return Task.FromResult(session.LastOperation);
            }

            session.EndpointPlan = BuildEndpointPlan(session, requestedProtocol);
            session.Summary = session.Summary with
            {
                State = AdapterSessionState.Prepared
            };
            session.LastOperation = new AdapterOperationResult
            {
                Category = AdapterOperationResultCategory.Succeeded,
                Message = "Session prepared."
            };
            session.AppendStdout($"Session '{session.SessionId}' prepared for {requestedProtocol}.");
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
                    Code = "unsupported",
                    Warnings = session.Summary.Warnings
                };
                return session.LastOperation;
            }

            if (session.EndpointHost is not null)
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
            session.AppendStdout($"Starting Incursa HTTP/3 endpoint for session '{session.SessionId}'.");
            session.WriteSessionSnapshot();
        }

        IncursaHttp3EndpointHost endpointHost;
        try
        {
            if (!string.IsNullOrWhiteSpace(options.ForceEndpointStartFailureMessage))
            {
                throw new InvalidOperationException(options.ForceEndpointStartFailureMessage);
            }

            endpointHost = await IncursaHttp3EndpointHost.StartAsync(
                new IncursaHttp3EndpointOptions
                {
                    Port = session.EndpointPlan!.Port,
                    ImplementationId = options.ImplementationId,
                    ImplementationName = options.ImplementationName,
                    SessionId = session.SessionId,
                    Mode = "endpoint",
                    ReadinessProbePath = options.ReadinessProbePath,
                    TimeProvider = options.TimeProvider
                },
                cancellationToken: default);
        }
        catch (Exception exception) when (exception is PlatformNotSupportedException or Win32Exception or InvalidOperationException or IOException)
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
                session.AppendStderr(exception.Message);
                session.WriteSessionSnapshot();
                return session.LastOperation;
            }
        }

        lock (session.Gate)
        {
            session.EndpointHost = endpointHost;
            session.Endpoint = endpointHost.Endpoint;
            session.EndpointStartedAt = Now();
            session.WriteEndpointSnapshot();
        }

        var readiness = await WaitForReadinessAsync(session.EndpointPlan!);
        IncursaHttp3EndpointHost? hostToStop = null;
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
                session.AppendStdout($"Incursa HTTP/3 endpoint became ready on {session.Endpoint?.Authority}.");
            }
            else
            {
                hostToStop = session.EndpointHost;
                session.EndpointHost = null;
                session.EndpointStartedAt = null;

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
                session.AppendStderr(readiness.Message);
            }

            session.WriteSessionSnapshot();
        }

        if (hostToStop is not null)
        {
            await hostToStop.DisposeAsync();
        }

        lock (session.Gate)
        {
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
                    Message = "Incursa HTTP/3 protocol endpoint ready.",
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
                AdapterSessionState.Running or AdapterSessionState.Starting or AdapterSessionState.Prepared or AdapterSessionState.Stopped => new AdapterReadinessSnapshot
                {
                    Status = AdapterReadinessStatus.NotReady,
                    Message = "Endpoint is not ready yet.",
                    ObservedAt = Now(),
                    Warnings = session.Summary.Warnings
                },
                AdapterSessionState.Disposed => new AdapterReadinessSnapshot
                {
                    Status = AdapterReadinessStatus.Unknown,
                    Message = "Session has been disposed.",
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
                    Status = AdapterHealthStatus.Ready,
                    Message = "Incursa HTTP/3 adapter ready.",
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
            if (session.EndpointPlan is not null)
            {
                metrics.AddRange(CreateEndpointMetrics(session));
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
        IncursaHttp3EndpointHost? endpointHost;
        lock (session.Gate)
        {
            endpointHost = session.EndpointHost;
            session.Summary = session.Summary with
            {
                State = AdapterSessionState.Stopping,
                UpdatedAt = Now()
            };
            session.AppendStdout("Stopping Incursa HTTP/3 endpoint.");
            session.WriteSessionSnapshot();
        }

        if (endpointHost is not null)
        {
            await endpointHost.DisposeAsync();
        }

        lock (session.Gate)
        {
            session.EndpointHost = null;
            session.EndpointStartedAt = null;
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
            session.AppendStdout("Incursa HTTP/3 endpoint stopped.");
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

    private async Task StopIfNeededAsync(IncursaHttp3Session session)
    {
        IncursaHttp3EndpointHost? endpointHost;
        lock (session.Gate)
        {
            endpointHost = session.EndpointHost;
            session.EndpointHost = null;
            session.EndpointStartedAt = null;
        }

        if (endpointHost is not null)
        {
            await endpointHost.DisposeAsync();
        }
    }

    private IncursaHttp3Session GetOrCreateDeletedSession(string sessionId)
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
                return new IncursaHttp3Session(sessionId, CreateSessionDirectory(null, null, sessionId))
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

    private IncursaHttp3Session GetSession(string sessionId)
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
                Id = "http3.server",
                Status = AdapterCapabilityStatus.Supported,
                Description = "HTTP/3 endpoint support via Incursa HTTP/3."
            },
            new AdapterCapability
            {
                Id = "quic.server",
                Status = AdapterCapabilityStatus.Supported,
                Description = "QUIC transport support via the Incursa QUIC implementation."
            },
            new AdapterCapability
            {
                Id = "httpPlaintext",
                Status = AdapterCapabilityStatus.Supported,
                Description = "GET /plaintext."
            },
            new AdapterCapability
            {
                Id = "httpJson",
                Status = AdapterCapabilityStatus.Supported,
                Description = "GET /json."
            },
            new AdapterCapability
            {
                Id = "httpStatus",
                Status = AdapterCapabilityStatus.Supported,
                Description = "GET /status."
            },
            new AdapterCapability
            {
                Id = "httpBytes",
                Status = AdapterCapabilityStatus.Supported,
                Description = "GET /bytes/{size}."
            },
            new AdapterCapability
            {
                Id = "httpStreaming",
                Status = AdapterCapabilityStatus.Supported,
                Description = "GET /stream/bytes."
            },
            new AdapterCapability
            {
                Id = "httpUpload",
                Status = AdapterCapabilityStatus.Supported,
                Description = "POST /echo, /hash, /sink, and /upload."
            },
            new AdapterCapability
            {
                Id = "httpHeaders",
                Status = AdapterCapabilityStatus.Supported,
                Description = "GET /headers/response and /inspect/headers."
            }
        ];
    }

    private IncursaHttp3ScenarioSupport EvaluateSupport(ScenarioDefinition scenario, string requestedProtocol)
    {
        var warnings = new List<string>();

        var declaredProtocol = NormalizeProtocolInternal(scenario.Protocol);
        var normalizedRequestedProtocol = NormalizeProtocolInternal(requestedProtocol);
        if (!string.IsNullOrWhiteSpace(scenario.Protocol) &&
            !string.Equals(declaredProtocol, normalizedRequestedProtocol, StringComparison.OrdinalIgnoreCase))
        {
            return IncursaHttp3ScenarioSupport.Unsupported($"The scenario protocol '{scenario.Protocol}' does not match the requested endpoint type '{requestedProtocol}'.", warnings);
        }

        if (!string.Equals(scenario.ImplementationRole, "server", StringComparison.OrdinalIgnoreCase))
        {
            return IncursaHttp3ScenarioSupport.Unsupported("The Incursa HTTP/3 adapter only supports server scenarios.", warnings);
        }

        var endpoint = ResolveScenarioEndpoint(scenario);
        if (endpoint is null)
        {
            return IncursaHttp3ScenarioSupport.Unsupported("The scenario does not declare an HTTP endpoint.", warnings);
        }

        if (!string.Equals(scenario.Family, "http.application", StringComparison.OrdinalIgnoreCase) &&
            !scenario.Id.StartsWith("http.", StringComparison.OrdinalIgnoreCase) &&
            !scenario.Id.StartsWith("fixture.incursa-http3.", StringComparison.OrdinalIgnoreCase))
        {
            return IncursaHttp3ScenarioSupport.Unsupported("The scenario is outside the Incursa HTTP/3 adapter's coverage.", warnings);
        }

        if (!SupportedHttpMethods.Contains(endpoint.Method))
        {
            return IncursaHttp3ScenarioSupport.Unsupported($"The HTTP method '{endpoint.Method}' is not supported.", warnings);
        }

        if (!IsSupportedPath(endpoint.Path))
        {
            return IncursaHttp3ScenarioSupport.Unsupported($"The HTTP path '{endpoint.Path}' is not supported by the Incursa HTTP/3 adapter.", warnings);
        }

        foreach (var capability in scenario.RequiredCapabilities)
        {
            if (!SupportsCapability(capability))
            {
                return IncursaHttp3ScenarioSupport.Unsupported($"The required capability '{capability}' is not supported.", warnings);
            }
        }

        if (!string.Equals(requestedProtocol, "h3", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requestedProtocol, "http3", StringComparison.OrdinalIgnoreCase))
        {
            return IncursaHttp3ScenarioSupport.Unsupported($"The Incursa HTTP/3 adapter only supports HTTP/3 ('h3' or 'http3'), but '{requestedProtocol}' was requested.", warnings);
        }

        return IncursaHttp3ScenarioSupport.Supported(warnings);
    }

    private static bool SupportsCapability(string capability)
    {
        return SupportedCapabilities.Contains(capability);
    }

    private static HttpEndpointSpec? ResolveScenarioEndpoint(ScenarioDefinition scenario)
    {
        if (scenario.Endpoint is not null)
        {
            return scenario.Endpoint;
        }

        return scenario.Id.ToLowerInvariant() switch
        {
            "http.core.plaintext" or "fixture.incursa-http3.plaintext" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/plaintext"
            },
            "http.core.json" or "fixture.incursa-http3.json" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/json"
            },
            "http.core.status" or "fixture.incursa-http3.status" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/status"
            },
            "http.payload.bytes.1kb" or "fixture.incursa-http3.bytes-1kb" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/bytes/1024"
            },
            "http.payload.bytes.64kb" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/bytes/65536"
            },
            "http.payload.bytes.1mb" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/bytes/1048576"
            },
            "http.payload.stream.100x16kb" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/stream/bytes",
                Query = new Dictionary<string, string>
                {
                    ["chunks"] = "100",
                    ["size"] = "16384",
                    ["delayMs"] = "0"
                }
            },
            "http.headers.response.50x32" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/headers/response",
                Query = new Dictionary<string, string>
                {
                    ["count"] = "50",
                    ["size"] = "32"
                }
            },
            "http.headers.inspect-request" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/inspect/headers"
            },
            "http.upload.echo.64kb" => new HttpEndpointSpec
            {
                Method = "POST",
                Path = "/echo",
                RequestBodyGeneration = "deterministic-bytes:65536"
            },
            "http.upload.hash.1mb" => new HttpEndpointSpec
            {
                Method = "POST",
                Path = "/hash",
                RequestBodyGeneration = "deterministic-bytes:1048576"
            },
            "http.upload.sink.1mb" => new HttpEndpointSpec
            {
                Method = "POST",
                Path = "/sink",
                RequestBodyGeneration = "deterministic-bytes:1048576"
            },
            _ => null
        };
    }

    private static bool IsSupportedPath(string path)
    {
        return string.Equals(path, "/plaintext", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/status", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/stream/bytes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/headers/response", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/inspect/headers", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/echo", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/hash", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/sink", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/upload", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/bytes/", StringComparison.OrdinalIgnoreCase);
    }

    private IncursaHttp3EndpointPlan BuildEndpointPlan(IncursaHttp3Session session, string requestedProtocol)
    {
        var protocol = NormalizeProtocolInternal(requestedProtocol);
        if (protocol is null || !string.Equals(protocol, "h3", StringComparison.OrdinalIgnoreCase))
        {
            throw CreateProblem(
                "unsupported",
                StatusCodes.Status422UnprocessableEntity,
                $"The Incursa HTTP/3 adapter only supports HTTP/3 ('h3' or 'http3'), but '{requestedProtocol}' was requested.",
                operation: "prepare",
                sessionId: session.SessionId);
        }

        var controlPlanePort = TryGetPort(options.ControlPlaneBaseUrl);
        var endpointPort = GetFreeUdpPort(controlPlanePort);

        return new IncursaHttp3EndpointPlan(
            protocol,
            "https",
            $"https://127.0.0.1:{endpointPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            endpointPort);
    }

    private static string? NormalizeProtocolInternal(string? requestedProtocol)
    {
        if (string.Equals(requestedProtocol, "h3", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedProtocol, "http3", StringComparison.OrdinalIgnoreCase))
        {
            return "h3";
        }

        if (string.Equals(requestedProtocol, "h1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedProtocol, "http1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedProtocol, "h2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedProtocol, "http2", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return null;
    }

    private async Task<ReadinessResult> WaitForReadinessAsync(IncursaHttp3EndpointPlan plan)
    {
        var deadline = Now() + options.ReadinessTimeout;
        var errors = new List<string>();

        while (Now() < deadline)
        {
            try
            {
                if (await ProbeReadyAsync(plan))
                {
                    return ReadinessResult.Ready(errors);
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException or NotSupportedException)
            {
                errors.Add(exception.Message);
            }

            await Task.Delay(500);
        }

        var message = errors.Count == 0
            ? $"The Incursa HTTP/3 endpoint did not become ready within {options.ReadinessTimeout.TotalSeconds:0} seconds."
            : $"The Incursa HTTP/3 endpoint did not become ready within {options.ReadinessTimeout.TotalSeconds:0} seconds. Last error: {errors[^1]}";
        return ReadinessResult.Failed(message, errors);
    }

    private async Task<bool> ProbeReadyAsync(IncursaHttp3EndpointPlan plan)
    {
        using var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(2),
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = static (_, _, _, _) => true
            }
        };

        using var client = new HttpClient(handler)
        {
            Timeout = options.HttpTimeout
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(plan.BaseUrl), options.ReadinessProbePath.TrimStart('/')))
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        if (response.Version.Major != 3)
        {
            return false;
        }

        return true;
    }

    private IReadOnlyList<AdapterArtifact> BuildArtifacts(IncursaHttp3Session session)
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
                Status = session.Endpoint is null ? AdapterResourceAvailability.Unavailable : AdapterResourceAvailability.Available,
                Path = session.EndpointSnapshotPath,
                ContentType = "application/json",
                Final = true
            });
        }

        return artifacts;
    }

    private IReadOnlyList<AdapterMetric> CreateEndpointMetrics(IncursaHttp3Session session)
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var metrics = new List<AdapterMetric>
        {
            new()
            {
                MetricId = "session.state",
                Scope = "session",
                Value = JsonSerializer.SerializeToElement(session.Summary.State.ToString(), jsonOptions),
                Notes = "Current session state."
            }
        };

        if (session.EndpointPlan is not null)
        {
            metrics.Add(new AdapterMetric
            {
                MetricId = "endpoint.port",
                Scope = "endpoint",
                Value = JsonSerializer.SerializeToElement(session.EndpointPlan.Port, jsonOptions),
                Notes = "Protocol endpoint port."
            });
        }

        var sink = session.EndpointHost?.MetricsSink;
        metrics.Add(new AdapterMetric
        {
            MetricId = "endpoint.active-connections",
            Scope = "endpoint",
            Value = JsonSerializer.SerializeToElement(sink?.ActiveConnections ?? 0, jsonOptions),
            Notes = "Active QUIC connections."
        });
        metrics.Add(new AdapterMetric
        {
            MetricId = "endpoint.active-requests",
            Scope = "endpoint",
            Value = JsonSerializer.SerializeToElement(sink?.ActiveRequests ?? 0, jsonOptions),
            Notes = "Active HTTP/3 requests."
        });

        if (session.EndpointStartedAt is not null)
        {
            metrics.Add(new AdapterMetric
            {
                MetricId = "endpoint.uptime-seconds",
                Scope = "endpoint",
                Value = JsonSerializer.SerializeToElement((Now() - session.EndpointStartedAt.Value).TotalSeconds, jsonOptions),
                Notes = "Seconds since the protocol endpoint started."
            });
        }

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
        var segments = new List<string> { options.RepositoryRoot, ".artifacts", "runs" };
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
            return $"incursa-http3-adapter-{sessionCounter:0000}";
        }
    }

    private DateTimeOffset Now()
    {
        return options.TimeProvider.GetUtcNow();
    }

    private static int GetFreeUdpPort(int? avoidPort = null)
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetworkV6,
                System.Net.Sockets.SocketType.Dgram,
                System.Net.Sockets.ProtocolType.Udp);
            socket.DualMode = true;
            socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
            var port = ((IPEndPoint)socket.LocalEndPoint!).Port;

            if (!avoidPort.HasValue || port != avoidPort.Value)
            {
                return port;
            }
        }

        throw new InvalidOperationException("Unable to allocate a free protocol endpoint port distinct from the adapter control plane port.");
    }

    private static int? TryGetPort(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? uri.Port : null;
    }

    private static IncursaHttp3AdapterProblemException CreateProblem(string code, int status, string title, string operation, string? sessionId = null)
    {
        return new IncursaHttp3AdapterProblemException(new AdapterProblemDetails
        {
            Type = "https://incursa.example/problems/incursa-http3-adapter",
            Title = title,
            Status = status,
            Code = code,
            Operation = operation,
            SessionId = sessionId,
            Retryable = false
        }, (HttpStatusCode)status);
    }

    private static readonly HashSet<string> SupportedHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET",
        "POST"
    };

    private static readonly HashSet<string> SupportedCapabilities = new(StringComparer.OrdinalIgnoreCase)
    {
        "httpPlaintext",
        "httpJson",
        "httpStatus",
        "httpBytes",
        "httpStreaming",
        "httpUpload",
        "httpHeaders"
    };
}

public sealed class IncursaHttp3AdapterProblemException : Exception
{
    public IncursaHttp3AdapterProblemException(AdapterProblemDetails problem, HttpStatusCode statusCode)
        : base(problem.Title)
    {
        Problem = problem;
        StatusCode = statusCode;
    }

    public AdapterProblemDetails Problem { get; }

    public HttpStatusCode StatusCode { get; }
}

internal sealed class IncursaHttp3Session
{
    public IncursaHttp3Session(string sessionId, string sessionDirectory)
    {
        SessionId = sessionId;
        SessionDirectory = sessionDirectory;
        SessionSnapshotPath = Path.Combine(SessionDirectory, "session.json");
        EndpointSnapshotPath = Path.Combine(SessionDirectory, "endpoint.json");
        StdoutPath = Path.Combine(SessionDirectory, "server.stdout.txt");
        StderrPath = Path.Combine(SessionDirectory, "server.stderr.txt");
        CommandLinePath = Path.Combine(SessionDirectory, "server.command.txt");
        Directory.CreateDirectory(SessionDirectory);
        File.WriteAllText(StdoutPath, string.Empty);
        File.WriteAllText(StderrPath, string.Empty);
        File.WriteAllText(CommandLinePath, string.Empty);
    }

    public string SessionId { get; }

    public string SessionDirectory { get; }

    public string SessionSnapshotPath { get; }

    public string EndpointSnapshotPath { get; }

    public string StdoutPath { get; }

    public string StderrPath { get; }

    public string CommandLinePath { get; }

    public object Gate { get; } = new();

    public AdapterSessionSummary Summary { get; set; } = new();

    public AdapterOperationResult? LastOperation { get; set; }

    public ScenarioDefinition? Scenario { get; set; }

    public string? RequestedProtocol { get; set; }

    public IReadOnlyList<AdapterEndpointBinding> RequestedEndpointBindings { get; set; } = [];

    public IReadOnlyList<AdapterArtifactExpectation> ArtifactOutputExpectations { get; set; } = [];

    public Dictionary<string, JsonElement> Extensions { get; set; } = [];

    public IncursaHttp3EndpointPlan? EndpointPlan { get; set; }

    public AdapterEndpoint? Endpoint { get; set; }

    public DateTimeOffset? EndpointStartedAt { get; set; }

    public IncursaHttp3EndpointHost? EndpointHost { get; set; }

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
        File.WriteAllText(CommandLinePath, EndpointHost?.CommandLine ?? string.Empty);
    }

    public void AppendStdout(string message)
    {
        AppendLogLine(StdoutPath, message);
    }

    public void AppendStderr(string message)
    {
        AppendLogLine(StderrPath, message);
    }

    private static void AppendLogLine(string path, string message)
    {
        var line = $"[{DateTimeOffset.UtcNow:O}] {message}{Environment.NewLine}";
        File.AppendAllText(path, line, Encoding.UTF8);
    }
}

internal sealed record IncursaHttp3EndpointPlan(
    string Protocol,
    string Scheme,
    string BaseUrl,
    int Port);

internal sealed record ReadinessResult(bool IsReady, bool IsUnsupported, string Message, IReadOnlyList<string> Warnings)
{
    public static ReadinessResult Ready(IReadOnlyList<string> warnings) => new(true, false, "Incursa HTTP/3 endpoint is ready.", warnings);

    public static ReadinessResult Failed(string message, IReadOnlyList<string> warnings) => new(false, false, message, warnings);

    public static ReadinessResult Unsupported(string message, IReadOnlyList<string> warnings) => new(false, true, message, warnings);
}

internal sealed record IncursaHttp3ScenarioSupport(bool IsSupported, string? Reason, IReadOnlyList<string> Warnings)
{
    public static IncursaHttp3ScenarioSupport Supported(IReadOnlyList<string> warnings) => new(true, null, warnings);

    public static IncursaHttp3ScenarioSupport Unsupported(string reason, IReadOnlyList<string> warnings) => new(false, reason, warnings);
}
