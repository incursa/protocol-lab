// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Quic;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Incursa.ProtocolLab.Adapters.Kestrel;

public sealed record KestrelAdapterOptions
{
    public string RepositoryRoot { get; init; } = Directory.GetCurrentDirectory();

    public string ControlPlaneBaseUrl { get; init; } = "";

    public string AdapterIdentityId { get; init; } = "kestrel-adapter-v1";

    public string AdapterIdentityName { get; init; } = "Kestrel Adapter v1";

    public string AdapterIdentityVersion { get; init; } = "1.0.0";

    public string ImplementationId { get; init; } = "kestrel-http3";

    public string ImplementationName { get; init; } = "Kestrel HTTP/3";

    public string ImplementationVersion { get; init; } = "1.0.0";

    public string ImplementationImage { get; init; } = "";

    public string ContractVersion { get; init; } = "v1";

    public string SupportedScenarioSelectorExpression { get; init; } =
        "http.core.*|http.payload.*|http.upload.*|http.headers.*|fixture.kestrel.*";

    public string SupportedEndpointPathExpression { get; init; } =
        "/plaintext|/json|/status|/bytes/*|/stream/bytes|/sink|/hash|/echo|/headers/response|/inspect/headers";

    public string BenchmarkServerProjectPath { get; init; } = Path.Combine(
        "servers",
        "KestrelBenchServer",
        "KestrelBenchServer.csproj");

    public TimeSpan StartTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan ReadinessTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public string DefaultControlPlaneContentType { get; init; } = "application/json";

    public string ReadinessProbePath { get; init; } = "/plaintext";

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}

public sealed class KestrelAdapterRuntime
{
    private readonly KestrelAdapterOptions options;
    private readonly ConcurrentDictionary<string, KestrelSession> sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> deletedSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();
    private int sessionCounter;

    public KestrelAdapterRuntime(KestrelAdapterOptions options)
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
            Message = "Kestrel adapter ready.",
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
            ProcessMetricsAvailable = true,
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
                    Description = "HTTP application scenarios supported by the Kestrel benchmark server."
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
                    Type = "http",
                    Description = "Plain HTTP control-plane-selected endpoint.",
                    Protocols = ["h1"]
                },
                new AdapterEndpointType
                {
                    Type = "https",
                    Description = "Loopback HTTPS endpoint for HTTP/2 and HTTP/3 sessions.",
                    Protocols = ["h2", "h3"]
                }
            ],
            SupportedArtifactTypes =
            [
                new AdapterArtifactType
                {
                    Type = "stdout",
                    Description = "Kestrel benchmark server stdout capture.",
                    ProducedByStates = ["starting", "running", "stopping", "stopped"]
                },
                new AdapterArtifactType
                {
                    Type = "stderr",
                    Description = "Kestrel benchmark server stderr capture.",
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
            var session = new KestrelSession(sessionId, CreateSessionDirectory(request.RunId, request.CellId, sessionId))
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

            if (session.EndpointProcess is not null)
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

        KestrelEndpointProcess endpointProcess;
        try
        {
            endpointProcess = await KestrelProtocolEndpointLauncher.StartAsync(
                options.RepositoryRoot,
                session,
                session.EndpointPlan!,
                options.BenchmarkServerProjectPath,
                options.TimeProvider,
                options.HttpTimeout,
                cancellationToken: default);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
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
            session.EndpointProcess = endpointProcess;
            session.Endpoint = endpointProcess.Endpoint;
            session.WriteEndpointSnapshot();
        }

        var readiness = await WaitForReadinessAsync(session, endpointProcess, session.EndpointPlan!);
        KestrelEndpointProcess? processToStop = null;
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
                processToStop = session.EndpointProcess;
                session.EndpointProcess = null;

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

        if (processToStop is not null)
        {
            await processToStop.StopAsync();
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
                    Message = "Kestrel protocol endpoint ready.",
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
                    Status = AdapterHealthStatus.Ready,
                    Message = "Kestrel adapter ready.",
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
            if (session.EndpointProcess is not null)
            {
                metrics.AddRange(CreateProcessMetrics(session.EndpointProcess));
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
        KestrelEndpointProcess? endpointProcess;
        lock (session.Gate)
        {
            endpointProcess = session.EndpointProcess;
            session.Summary = session.Summary with
            {
                State = AdapterSessionState.Stopping,
                UpdatedAt = Now()
            };
            session.WriteSessionSnapshot();
        }

        if (endpointProcess is not null)
        {
            await endpointProcess.StopAsync();
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

    private async Task StopIfNeededAsync(KestrelSession session)
    {
        KestrelEndpointProcess? endpointProcess;
        lock (session.Gate)
        {
            endpointProcess = session.EndpointProcess;
            session.EndpointProcess = null;
        }

        if (endpointProcess is not null)
        {
            await endpointProcess.StopAsync();
        }
    }

    private KestrelSession GetOrCreateDeletedSession(string sessionId)
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
                return new KestrelSession(sessionId, CreateSessionDirectory(null, null, sessionId))
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

    private KestrelSession GetSession(string sessionId)
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
                Id = "http1.server",
                Status = AdapterCapabilityStatus.Supported,
                Description = "HTTP/1.1 Kestrel endpoint support."
            },
            new AdapterCapability
            {
                Id = "http2.server",
                Status = AdapterCapabilityStatus.Supported,
                Description = "HTTP/2 Kestrel endpoint support over HTTPS."
            },
            new AdapterCapability
            {
                Id = "http3.server",
                Status = QuicConnection.IsSupported ? AdapterCapabilityStatus.Supported : AdapterCapabilityStatus.Conditional,
                Description = QuicConnection.IsSupported
                    ? "HTTP/3 Kestrel endpoint support over HTTPS and QUIC."
                    : "HTTP/3 is conditional on the local runtime's QUIC support."
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
                Description = "POST /sink, /hash, and /echo."
            },
            new AdapterCapability
            {
                Id = "httpHeaders",
                Status = AdapterCapabilityStatus.Supported,
                Description = "GET /headers/response and /inspect/headers."
            }
        ];
    }

    private KestrelScenarioSupport EvaluateSupport(ScenarioDefinition scenario, string requestedProtocol)
    {
        var warnings = new List<string>();

        var declaredProtocol = KestrelProtocolSupport.NormalizeProtocol(scenario.Protocol);
        var normalizedRequestedProtocol = KestrelProtocolSupport.NormalizeProtocol(requestedProtocol);
        if (!string.IsNullOrWhiteSpace(scenario.Protocol) &&
            !string.Equals(declaredProtocol, normalizedRequestedProtocol, StringComparison.OrdinalIgnoreCase))
        {
            return KestrelScenarioSupport.Unsupported($"The scenario protocol '{scenario.Protocol}' does not match the requested endpoint type '{requestedProtocol}'.", warnings);
        }

        if (!string.Equals(scenario.ImplementationRole, "server", StringComparison.OrdinalIgnoreCase))
        {
            return KestrelScenarioSupport.Unsupported("The Kestrel adapter only supports server scenarios.", warnings);
        }

        var endpoint = ResolveScenarioEndpoint(scenario);
        if (endpoint is null)
        {
            return KestrelScenarioSupport.Unsupported("The scenario does not declare an HTTP endpoint.", warnings);
        }

        if (!string.Equals(scenario.Family, "http.application", StringComparison.OrdinalIgnoreCase) &&
            !scenario.Id.StartsWith("http.", StringComparison.OrdinalIgnoreCase) &&
            !scenario.Id.StartsWith("fixture.kestrel.", StringComparison.OrdinalIgnoreCase))
        {
            return KestrelScenarioSupport.Unsupported("The scenario is outside the Kestrel adapter's HTTP coverage.", warnings);
        }

            if (!SupportedHttpMethods.Contains(endpoint.Method))
            {
                return KestrelScenarioSupport.Unsupported($"The HTTP method '{endpoint.Method}' is not supported.", warnings);
            }

        if (!IsSupportedPath(endpoint.Path))
        {
            return KestrelScenarioSupport.Unsupported($"The HTTP path '{endpoint.Path}' is not supported.", warnings);
        }

        foreach (var capability in scenario.RequiredCapabilities)
        {
            if (!SupportsCapability(capability))
            {
                return KestrelScenarioSupport.Unsupported($"The required capability '{capability}' is not supported.", warnings);
            }
        }

        if (string.Equals(requestedProtocol, "h3", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedProtocol, "http3", StringComparison.OrdinalIgnoreCase))
        {
            if (!QuicConnection.IsSupported)
            {
                return KestrelScenarioSupport.Unsupported("HTTP/3 is not supported by the local runtime.", warnings);
            }
        }

        if (!string.Equals(requestedProtocol, "h1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requestedProtocol, "h2", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requestedProtocol, "h3", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requestedProtocol, "http1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requestedProtocol, "http2", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requestedProtocol, "http3", StringComparison.OrdinalIgnoreCase))
        {
            return KestrelScenarioSupport.Unsupported($"The requested endpoint type '{requestedProtocol}' is not supported.", warnings);
        }

        return KestrelScenarioSupport.Supported(warnings);
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
            "fixture.kestrel.success" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/plaintext"
            },
            "fixture.kestrel.json" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/json"
            },
            "fixture.kestrel.status" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/status"
            },
            "fixture.kestrel.bytes" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/bytes/1024"
            },
            "fixture.kestrel.stream-bytes" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/stream/bytes"
            },
            "fixture.kestrel.sink" => new HttpEndpointSpec
            {
                Method = "POST",
                Path = "/sink"
            },
            "fixture.kestrel.hash" => new HttpEndpointSpec
            {
                Method = "POST",
                Path = "/hash"
            },
            "fixture.kestrel.echo" => new HttpEndpointSpec
            {
                Method = "POST",
                Path = "/echo"
            },
            "fixture.kestrel.response-headers" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/headers/response"
            },
            "fixture.kestrel.inspect-headers" => new HttpEndpointSpec
            {
                Method = "GET",
                Path = "/inspect/headers"
            },
            _ => null
        };
    }

    private static bool IsSupportedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (string.Equals(path, "/plaintext", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/status", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/stream/bytes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/sink", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/hash", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/echo", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/headers/response", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/inspect/headers", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith("/bytes/", StringComparison.OrdinalIgnoreCase);
    }

    private AdapterEndpointPlan BuildEndpointPlan(KestrelSession session, string requestedProtocol)
    {
        var protocol = NormalizeProtocol(requestedProtocol);
        if (protocol is null)
        {
            throw CreateProblem(
                "unsupported",
                StatusCodes.Status422UnprocessableEntity,
                $"The requested endpoint type '{requestedProtocol}' is not supported.",
                operation: "prepare",
                sessionId: session.SessionId);
        }

        var controlPlanePort = TryGetPort(options.ControlPlaneBaseUrl);
        var endpointPort = GetFreePort(controlPlanePort);
        if (protocol == "h1")
        {
            return new AdapterEndpointPlan(
                protocol,
                "http",
                $"http://127.0.0.1:{endpointPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                endpointPort,
                HttpProtocols.Http1,
                false);
        }

        var httpsProtocols = protocol == "h2"
            ? HttpProtocols.Http2
            : HttpProtocols.Http1AndHttp2AndHttp3;

        return new AdapterEndpointPlan(
            protocol,
            "https",
            $"https://127.0.0.1:{endpointPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            endpointPort,
            httpsProtocols,
            protocol == "h3");
    }

    private static string? NormalizeProtocol(string requestedProtocol)
    {
        if (string.Equals(requestedProtocol, "h1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedProtocol, "http1", StringComparison.OrdinalIgnoreCase))
        {
            return "h1";
        }

        if (string.Equals(requestedProtocol, "h2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedProtocol, "http2", StringComparison.OrdinalIgnoreCase))
        {
            return "h2";
        }

        if (string.Equals(requestedProtocol, "h3", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedProtocol, "http3", StringComparison.OrdinalIgnoreCase))
        {
            return "h3";
        }

        return null;
    }

    private async Task<ReadinessResult> WaitForReadinessAsync(KestrelSession session, KestrelEndpointProcess process, AdapterEndpointPlan plan)
    {
        var deadline = Now() + options.ReadinessTimeout;
        var errors = new List<string>();

        while (Now() < deadline)
        {
            if (process.HasExited)
            {
                return ReadinessResult.Failed($"The benchmark server exited before readiness with code {process.ExitCode}.", errors);
            }

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

            await Task.Delay(250);
        }

        var message = errors.Count == 0
            ? $"The benchmark server did not become ready within {options.ReadinessTimeout.TotalSeconds:0} seconds."
            : $"The benchmark server did not become ready within {options.ReadinessTimeout.TotalSeconds:0} seconds. Last error: {errors[^1]}";
        return ReadinessResult.Failed(message, errors);
    }

    private async Task<bool> ProbeReadyAsync(AdapterEndpointPlan plan)
    {
        using var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(2)
        };

        if (string.Equals(plan.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            handler.SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = static (_, _, _, _) => true
            };
        }

        using var client = new HttpClient(handler)
        {
            Timeout = options.HttpTimeout
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(plan.BaseUrl), options.ReadinessProbePath.TrimStart('/')))
        {
            Version = plan.HttpProtocols == HttpProtocols.Http2
                ? HttpVersion.Version20
                : plan.HttpProtocols == HttpProtocols.Http1AndHttp2AndHttp3 && string.Equals(plan.Protocol, "h3", StringComparison.OrdinalIgnoreCase)
                    ? HttpVersion.Version30
                    : HttpVersion.Version11,
            VersionPolicy = plan.Protocol == "h3"
                ? HttpVersionPolicy.RequestVersionExact
                : plan.Protocol == "h2"
                    ? HttpVersionPolicy.RequestVersionExact
                    : HttpVersionPolicy.RequestVersionOrLower
        };

        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        if (plan.Protocol == "h3" && response.Version.Major != 3)
        {
            return false;
        }

        if (plan.Protocol == "h2" && response.Version.Major != 2)
        {
            return false;
        }

        return true;
    }

    private IReadOnlyList<AdapterArtifact> BuildArtifacts(KestrelSession session)
    {
        var artifacts = new List<AdapterArtifact>
        {
            new()
            {
                ArtifactId = "server.stdout",
                ArtifactType = "stdout",
                Status = session.EndpointProcess is null ? AdapterResourceAvailability.Unavailable : AdapterResourceAvailability.Available,
                Path = session.StdoutPath,
                ContentType = "text/plain",
                Final = true
            },
            new()
            {
                ArtifactId = "server.stderr",
                ArtifactType = "stderr",
                Status = session.EndpointProcess is null ? AdapterResourceAvailability.Unavailable : AdapterResourceAvailability.Available,
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

        return artifacts;
    }

    private IReadOnlyList<AdapterMetric> CreateProcessMetrics(KestrelEndpointProcess process)
    {
        process.Refresh();
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return
        [
            new AdapterMetric
            {
                MetricId = "process.id",
                Scope = "session",
                Value = JsonSerializer.SerializeToElement(process.ProcessId, jsonOptions),
                Notes = "Child benchmark server process id."
            },
            new AdapterMetric
            {
                MetricId = "process.working-set-bytes",
                Scope = "process",
                Value = JsonSerializer.SerializeToElement(process.WorkingSetBytes ?? 0L, jsonOptions),
                Notes = "Child benchmark server working set in bytes."
            },
            new AdapterMetric
            {
                MetricId = "process.cpu-seconds",
                Scope = "process",
                Value = JsonSerializer.SerializeToElement(process.CpuSeconds ?? 0d, jsonOptions),
                Notes = "Child benchmark server accumulated CPU time."
            },
            new AdapterMetric
            {
                MetricId = "endpoint.port",
                Scope = "endpoint",
                Value = JsonSerializer.SerializeToElement(process.Endpoint.Port, jsonOptions),
                Notes = "Protocol endpoint port."
            }
        ];
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
            return $"kestrel-adapter-{sessionCounter:0000}";
        }
    }

    private DateTimeOffset Now()
    {
        return options.TimeProvider.GetUtcNow();
    }

    private static int GetFreePort(int? avoidPort = null)
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

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

    private static KestrelAdapterProblemException CreateProblem(string code, int status, string title, string operation, string? sessionId = null)
    {
        return new KestrelAdapterProblemException(new AdapterProblemDetails
        {
            Type = "https://incursa.example/problems/kestrel-adapter",
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

public sealed class KestrelAdapterProblemException : Exception
{
    public KestrelAdapterProblemException(AdapterProblemDetails problem, HttpStatusCode statusCode)
        : base(problem.Title)
    {
        Problem = problem;
        StatusCode = statusCode;
    }

    public AdapterProblemDetails Problem { get; }

    public HttpStatusCode StatusCode { get; }
}

internal sealed class KestrelSession
{
    public KestrelSession(string sessionId, string sessionDirectory)
    {
        SessionId = sessionId;
        SessionDirectory = sessionDirectory;
        SessionSnapshotPath = Path.Combine(SessionDirectory, "session.json");
        EndpointSnapshotPath = Path.Combine(SessionDirectory, "endpoint.json");
        StdoutPath = Path.Combine(SessionDirectory, "server.stdout.txt");
        StderrPath = Path.Combine(SessionDirectory, "server.stderr.txt");
        CommandLinePath = Path.Combine(SessionDirectory, "server.command.txt");
        Directory.CreateDirectory(SessionDirectory);
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

    public AdapterEndpointPlan? EndpointPlan { get; set; }

    public AdapterEndpoint? Endpoint { get; set; }

    public KestrelEndpointProcess? EndpointProcess { get; set; }

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
        File.WriteAllText(CommandLinePath, EndpointProcess?.CommandLine ?? string.Empty);
    }
}

internal sealed record AdapterEndpointPlan(
    string Protocol,
    string Scheme,
    string BaseUrl,
    int Port,
    HttpProtocols HttpProtocols,
    bool RequiresQuicSupport);

internal sealed record ReadinessResult(bool IsReady, bool IsUnsupported, string Message, IReadOnlyList<string> Warnings)
{
    public static ReadinessResult Ready(IReadOnlyList<string> warnings) => new(true, false, "Kestrel benchmark server is ready.", warnings);

    public static ReadinessResult Failed(string message, IReadOnlyList<string> warnings) => new(false, false, message, warnings);

    public static ReadinessResult Unsupported(string message, IReadOnlyList<string> warnings) => new(false, true, message, warnings);
}

internal sealed record KestrelScenarioSupport(bool IsSupported, string? Reason, IReadOnlyList<string> Warnings)
{
    public static KestrelScenarioSupport Supported(IReadOnlyList<string> warnings) => new(true, null, warnings);

    public static KestrelScenarioSupport Unsupported(string reason, IReadOnlyList<string> warnings) => new(false, reason, warnings);
}

internal static class KestrelProtocolSupport
{
    public static string? NormalizeProtocol(string? requestedProtocol)
    {
        return requestedProtocol?.Trim().ToLowerInvariant() switch
        {
            "h1" or "http1" => "h1",
            "h2" or "http2" => "h2",
            "h3" or "http3" => "h3",
            _ => null
        };
    }
}
