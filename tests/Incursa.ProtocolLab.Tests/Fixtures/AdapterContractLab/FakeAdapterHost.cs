// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using Incursa.ProtocolLab.Adapter.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Incursa.ProtocolLab.Tests.Fixtures.AdapterContractLab;

public sealed record FakeAdapterHostOptions
{
    public string AdapterId { get; init; } = "fixture-adapter";

    public string AdapterName { get; init; } = "Fixture Adapter";

    public string ImplementationId { get; init; } = "fixture-implementation";

    public string ImplementationName { get; init; } = "Fixture Implementation";

    public string ContractVersion { get; init; } = "v1";

    public string[] SupportedRoles { get; init; } = ["server", "client"];

    public IReadOnlyDictionary<string, FakeAdapterScenarioProfile> ScenarioProfiles { get; init; } = new Dictionary<string, FakeAdapterScenarioProfile>(StringComparer.OrdinalIgnoreCase)
    {
        ["success"] = FakeAdapterScenarioProfile.Success("success"),
        ["unsupported"] = FakeAdapterScenarioProfile.Unsupported("unsupported"),
        ["prepare-failure"] = FakeAdapterScenarioProfile.PrepareFailure("prepare-failure"),
        ["start-failure"] = FakeAdapterScenarioProfile.StartFailure("start-failure"),
        ["readiness-failure"] = FakeAdapterScenarioProfile.ReadinessFailure("readiness-failure"),
        ["metrics"] = FakeAdapterScenarioProfile.MetricsProfile("metrics"),
        ["artifacts"] = FakeAdapterScenarioProfile.ArtifactsProfile("artifacts"),
        ["cleanup"] = FakeAdapterScenarioProfile.Cleanup("cleanup"),
        ["fixture.quic.handshake"] = FakeAdapterScenarioProfile.Success("fixture.quic.handshake") with
        {
            Endpoints = [FakeAdapterData.CreateQuicEndpoint()]
        },
        ["fixture.quic.bidirectional-echo"] = FakeAdapterScenarioProfile.Success("fixture.quic.bidirectional-echo") with
        {
            Endpoints = [FakeAdapterData.CreateQuicEndpoint()]
        },
        ["fixture.quic.bidirectional-bulk"] = FakeAdapterScenarioProfile.Success("fixture.quic.bidirectional-bulk") with
        {
            Endpoints = [FakeAdapterData.CreateQuicEndpoint()]
        },
        ["fixture.quic.unidirectional-send"] = FakeAdapterScenarioProfile.Success("fixture.quic.unidirectional-send") with
        {
            Endpoints = [FakeAdapterData.CreateQuicEndpoint()]
        },
        ["fixture.quic.unsupported"] = FakeAdapterScenarioProfile.Unsupported("fixture.quic.unsupported")
    };

    public FakeAdapterManifestBehavior ManifestBehavior { get; init; } = FakeAdapterManifestBehavior.Normal;

    public FakeAdapterHealthStatusBehavior HealthBehavior { get; init; } = FakeAdapterHealthStatusBehavior.Ready;

    public bool UseRealNetworkServer { get; init; }

    public int ControlPlanePort { get; init; } = 0;

    public TimeSpan ResponseDelay { get; init; } = TimeSpan.Zero;

    public static FakeAdapterHostOptions CreateDefault() => new();
}

public enum FakeAdapterManifestBehavior
{
    Normal,
    Problem,
    Malformed
}

public enum FakeAdapterHealthStatusBehavior
{
    Ready,
    Degraded
}

public enum FakeAdapterScenarioMode
{
    Success,
    Unsupported,
    PrepareFailure,
    StartFailure,
    ReadinessFailure,
    Metrics,
    Artifacts,
    Cleanup
}

public sealed record FakeAdapterScenarioProfile
{
    public string ScenarioId { get; init; } = "";

    public string ScenarioVersion { get; init; } = "1.0";

    public string Role { get; init; } = "server";

    public FakeAdapterScenarioMode Mode { get; init; } = FakeAdapterScenarioMode.Success;

    public string? Reason { get; init; }

    public IReadOnlyList<AdapterEndpoint> Endpoints { get; init; } = [FakeAdapterData.CreateQuicEndpoint()];

    public IReadOnlyList<AdapterMetric> Metrics { get; init; } = [FakeAdapterData.CreateMetric("fixture.metric.requests", "session", 42)];

    public IReadOnlyList<AdapterArtifact> Artifacts { get; init; } = [FakeAdapterData.CreateArtifact("fixture.log", "log", "/tmp/fixture.log")];

    public static FakeAdapterScenarioProfile Success(string scenarioId) => new()
    {
        ScenarioId = scenarioId,
        Mode = FakeAdapterScenarioMode.Success
    };

    public static FakeAdapterScenarioProfile Unsupported(string scenarioId) => new()
    {
        ScenarioId = scenarioId,
        Mode = FakeAdapterScenarioMode.Unsupported,
        Reason = "The requested scenario is intentionally unsupported by the fake adapter."
    };

    public static FakeAdapterScenarioProfile PrepareFailure(string scenarioId) => new()
    {
        ScenarioId = scenarioId,
        Mode = FakeAdapterScenarioMode.PrepareFailure,
        Reason = "The prepare step is intentionally configured to fail."
    };

    public static FakeAdapterScenarioProfile StartFailure(string scenarioId) => new()
    {
        ScenarioId = scenarioId,
        Mode = FakeAdapterScenarioMode.StartFailure,
        Reason = "The start step is intentionally configured to fail."
    };

    public static FakeAdapterScenarioProfile ReadinessFailure(string scenarioId) => new()
    {
        ScenarioId = scenarioId,
        Mode = FakeAdapterScenarioMode.ReadinessFailure,
        Reason = "The session never becomes ready."
    };

    public static FakeAdapterScenarioProfile MetricsProfile(string scenarioId) => new()
    {
        ScenarioId = scenarioId,
        Mode = FakeAdapterScenarioMode.Metrics
    };

    public static FakeAdapterScenarioProfile ArtifactsProfile(string scenarioId) => new()
    {
        ScenarioId = scenarioId,
        Mode = FakeAdapterScenarioMode.Artifacts
    };

    public static FakeAdapterScenarioProfile Cleanup(string scenarioId) => new()
    {
        ScenarioId = scenarioId,
        Mode = FakeAdapterScenarioMode.Cleanup
    };
}

public sealed class FakeAdapterHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly FakeAdapterHostOptions _options;
    private readonly Dictionary<string, FakeAdapterSessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _deletedSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private int _sessionCounter;

    private FakeAdapterHost(WebApplication app, HttpClient client, FakeAdapterHostOptions options)
    {
        _app = app;
        Client = client;
        _options = options;
    }

    public HttpClient Client { get; private set; }

    public static async Task<FakeAdapterHost> StartAsync(FakeAdapterHostOptions? options = null)
    {
        var resolvedOptions = options ?? FakeAdapterHostOptions.CreateDefault();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FakeAdapterHost).Assembly.FullName,
            EnvironmentName = Environments.Development
        });

        if (resolvedOptions.UseRealNetworkServer)
        {
            builder.WebHost.ConfigureKestrel(options =>
            {
                if (resolvedOptions.ControlPlanePort > 0)
                {
                    options.Listen(System.Net.IPAddress.Loopback, resolvedOptions.ControlPlanePort);
                }
                else
                {
                    options.Listen(System.Net.IPAddress.Loopback, 0);
                }
            });
        }
        else
        {
            builder.WebHost.UseTestServer();
        }

        var app = builder.Build();
        var host = new FakeAdapterHost(app, new HttpClient(), resolvedOptions);
        host.MapRoutes(app);
        await app.StartAsync();

        host.Client.Dispose();
        host.Client = resolvedOptions.UseRealNetworkServer
            ? new HttpClient
            {
                BaseAddress = new Uri(GetControlPlaneBaseAddress(app))
            }
            : app.GetTestClient();
        if (!resolvedOptions.UseRealNetworkServer)
        {
            host.Client.BaseAddress = new Uri("http://fixture.local");
        }
        return host;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup(AdapterRoutes.Prefix);
        group.MapGet("/health", GetHealthAsync);
        group.MapGet("/manifest", GetManifestAsync);
        group.MapPost("/sessions", CreateSessionAsync);
        group.MapGet("/sessions/{sessionId}", GetSessionAsync);
        group.MapPost("/sessions/{sessionId}/prepare", PrepareSessionAsync);
        group.MapPost("/sessions/{sessionId}/start", StartSessionAsync);
        group.MapGet("/sessions/{sessionId}/status", GetStatusAsync);
        group.MapGet("/sessions/{sessionId}/endpoints", GetEndpointsAsync);
        group.MapGet("/sessions/{sessionId}/metrics", GetMetricsAsync);
        group.MapGet("/sessions/{sessionId}/artifacts", GetArtifactsAsync);
        group.MapPost("/sessions/{sessionId}/stop", StopSessionAsync);
        group.MapDelete("/sessions/{sessionId}", DeleteSessionAsync);
    }

    private IResult GetHealthAsync()
    {
        MaybeDelay();
        var status = _options.HealthBehavior == FakeAdapterHealthStatusBehavior.Ready
            ? AdapterHealthStatus.Ready
            : AdapterHealthStatus.Degraded;

        return Results.Json(new AdapterHealthResponse
        {
            AdapterIdentity = CreateAdapterIdentity(),
            Status = status,
            VersionCompatibility = CreateCompatibility(),
            Message = status == AdapterHealthStatus.Ready ? "fixture adapter ready" : "fixture adapter degraded",
            ObservedAt = DateTimeOffset.UtcNow,
            Capabilities = [CreateCapability("session-lifecycle"), CreateCapability("endpoint-discovery"), CreateCapability("metrics-snapshot")]
        }, ProtocolLabAdapterJson.Options);
    }

    private IResult GetManifestAsync()
    {
        MaybeDelay();
        if (_options.ManifestBehavior == FakeAdapterManifestBehavior.Problem)
        {
            return Results.Json(CreateProblem("manifest-unavailable", 503, "Fixture manifest unavailable.", "manifest"), ProtocolLabAdapterJson.Options, contentType: "application/problem+json", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (_options.ManifestBehavior == FakeAdapterManifestBehavior.Malformed)
        {
            return Results.Text("{\"manifest\":", "application/json", Encoding.UTF8, StatusCodes.Status200OK);
        }

        return Results.Json(CreateManifest(), ProtocolLabAdapterJson.Options);
    }

    private IResult CreateSessionAsync(AdapterSessionCreateRequest request)
    {
        MaybeDelay();
        lock (_gate)
        {
            var sessionId = !string.IsNullOrWhiteSpace(request.RequestedSessionId)
                ? request.RequestedSessionId!
                : $"session-{++_sessionCounter:0000}";

            _deletedSessions.Remove(sessionId);
            var session = new FakeAdapterSessionState(sessionId)
            {
                Summary = new AdapterSessionSummary
                {
                    SessionId = sessionId,
                    State = AdapterSessionState.Created,
                    RunId = request.RunId,
                    CellId = request.CellId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            };

            _sessions[sessionId] = session;
            return Results.Json(new AdapterSessionResource
            {
                Session = session.Summary,
                Operation = new AdapterOperationResult
                {
                    Category = AdapterOperationResultCategory.Succeeded,
                    Message = "Session created."
                }
            }, ProtocolLabAdapterJson.Options, statusCode: StatusCodes.Status201Created);
        }
    }

    private IResult GetSessionAsync(string sessionId)
    {
        MaybeDelay();
        if (!TryGetSession(sessionId, out var session))
        {
            return Problem(sessionId, "session-not-found", StatusCodes.Status404NotFound, "Unknown session.");
        }

        return Results.Json(new AdapterSessionResource
        {
            Session = session.Summary,
            Operation = session.LastOperation
        }, ProtocolLabAdapterJson.Options);
    }

    private IResult PrepareSessionAsync(string sessionId, AdapterPrepareRequest request)
    {
        MaybeDelay();
        if (!TryGetSession(sessionId, out var session))
        {
            return Problem(sessionId, "session-not-found", StatusCodes.Status404NotFound, "Unknown session.");
        }

        var profile = GetProfile(request.ScenarioId);
        session.Profile = profile;
        session.Summary = session.Summary with
        {
            ScenarioId = request.ScenarioId,
            ScenarioVersion = request.ScenarioVersion,
            Role = request.Role,
            RunId = request.RunId,
            CellId = request.CellId,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        session.LastOperation = profile.Mode switch
        {
            FakeAdapterScenarioMode.Unsupported => CreateOperation(AdapterOperationResultCategory.Unsupported, profile.Reason),
            FakeAdapterScenarioMode.PrepareFailure => CreateOperation(AdapterOperationResultCategory.Failed, profile.Reason, retryable: false),
            _ => CreateOperation(AdapterOperationResultCategory.Succeeded, "Session prepared.")
        };

        session.Summary = session.Summary with
        {
            State = profile.Mode switch
            {
                FakeAdapterScenarioMode.Unsupported => AdapterSessionState.Unsupported,
                FakeAdapterScenarioMode.PrepareFailure => AdapterSessionState.Failed,
                _ => AdapterSessionState.Prepared
            },
            UpdatedAt = DateTimeOffset.UtcNow,
            Warnings = profile.Mode == FakeAdapterScenarioMode.Unsupported && profile.Reason is not null
                ? [profile.Reason]
                : []
        };

        return Results.Json(new AdapterOperationResult
        {
            Category = session.LastOperation.Category,
            Message = session.LastOperation.Message,
            Code = session.LastOperation.Code,
            Retryable = session.LastOperation.Retryable,
            Warnings = session.LastOperation.Warnings
        }, ProtocolLabAdapterJson.Options);
    }

    private IResult StartSessionAsync(string sessionId)
    {
        MaybeDelay();
        if (!TryGetSession(sessionId, out var session))
        {
            return Problem(sessionId, "session-not-found", StatusCodes.Status404NotFound, "Unknown session.");
        }

        var profile = session.Profile ?? GetProfile(session.Summary.ScenarioId ?? string.Empty);
        if (session.Summary.State is AdapterSessionState.Created or AdapterSessionState.Unsupported)
        {
            session.LastOperation = CreateOperation(AdapterOperationResultCategory.Rejected, "Start requires a prepared session.", retryable: false);
            session.Summary = session.Summary with
            {
                State = AdapterSessionState.Failed,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            return Results.Json(CreateProblem(
                "invalid-transition",
                StatusCodes.Status409Conflict,
                "Start requires a prepared session.",
                "start",
                "The session must be prepared before start.",
                sessionId), ProtocolLabAdapterJson.Options, statusCode: StatusCodes.Status409Conflict, contentType: "application/problem+json");
        }

        session.LastOperation = profile.Mode switch
        {
            FakeAdapterScenarioMode.StartFailure => CreateOperation(AdapterOperationResultCategory.Failed, profile.Reason, retryable: false),
            FakeAdapterScenarioMode.Unsupported => CreateOperation(AdapterOperationResultCategory.Unsupported, profile.Reason),
            _ => CreateOperation(AdapterOperationResultCategory.Succeeded, "Session started.")
        };

        session.Summary = session.Summary with
        {
            State = profile.Mode switch
            {
                FakeAdapterScenarioMode.StartFailure => AdapterSessionState.Failed,
                FakeAdapterScenarioMode.Unsupported => AdapterSessionState.Unsupported,
                _ => AdapterSessionState.Running
            },
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return Results.Json(session.LastOperation, ProtocolLabAdapterJson.Options);
    }

    private IResult GetStatusAsync(string sessionId)
    {
        MaybeDelay();
        if (!TryGetSession(sessionId, out var session))
        {
            return Problem(sessionId, "session-not-found", StatusCodes.Status404NotFound, "Unknown session.");
        }

        var profile = session.Profile ?? GetProfile(session.Summary.ScenarioId ?? string.Empty);
        var readiness = profile.Mode switch
        {
            FakeAdapterScenarioMode.ReadinessFailure => AdapterReadinessStatus.NotReady,
            FakeAdapterScenarioMode.Unsupported => AdapterReadinessStatus.Unsupported,
            FakeAdapterScenarioMode.PrepareFailure or FakeAdapterScenarioMode.StartFailure => AdapterReadinessStatus.Failed,
            _ => AdapterReadinessStatus.Ready
        };

        var health = _options.HealthBehavior == FakeAdapterHealthStatusBehavior.Ready
            ? AdapterHealthStatus.Ready
            : AdapterHealthStatus.Degraded;

        return Results.Json(new AdapterStatusResponse
        {
            Session = session.Summary,
            Readiness = new AdapterReadinessSnapshot
            {
                Status = readiness,
                Message = readiness switch
                {
                    AdapterReadinessStatus.Ready => "fixture endpoint ready",
                    AdapterReadinessStatus.NotReady => "fixture endpoint not ready",
                    AdapterReadinessStatus.Unsupported => profile.Reason ?? "unsupported",
                    AdapterReadinessStatus.Failed => profile.Reason ?? "failed",
                    _ => null
                },
                ObservedAt = DateTimeOffset.UtcNow,
                Warnings = readiness == AdapterReadinessStatus.NotReady && profile.Reason is not null ? [profile.Reason] : []
            },
            Health = new AdapterHealthSnapshot
            {
                Status = health,
                Message = health == AdapterHealthStatus.Ready ? "fixture adapter ready" : "fixture adapter degraded",
                ObservedAt = DateTimeOffset.UtcNow
            },
            Operation = session.LastOperation
        }, ProtocolLabAdapterJson.Options);
    }

    private IResult GetEndpointsAsync(string sessionId)
    {
        MaybeDelay();
        if (!TryGetSession(sessionId, out var session))
        {
            return Problem(sessionId, "session-not-found", StatusCodes.Status404NotFound, "Unknown session.");
        }

        var profile = session.Profile ?? GetProfile(session.Summary.ScenarioId ?? string.Empty);
        var endpoints = profile.Mode == FakeAdapterScenarioMode.Unsupported
            ? []
            : profile.Endpoints;

        return Results.Json(new AdapterEndpointsResponse
        {
            Session = session.Summary,
            Endpoints = endpoints,
            Operation = CreateOperation(profile.Mode == FakeAdapterScenarioMode.Unsupported
                ? AdapterOperationResultCategory.Unsupported
                : AdapterOperationResultCategory.Succeeded, profile.Mode == FakeAdapterScenarioMode.Unsupported ? profile.Reason : "Endpoints discovered.")
        }, ProtocolLabAdapterJson.Options);
    }

    private IResult GetMetricsAsync(string sessionId)
    {
        MaybeDelay();
        if (!TryGetSession(sessionId, out var session))
        {
            return Problem(sessionId, "session-not-found", StatusCodes.Status404NotFound, "Unknown session.");
        }

        var profile = session.Profile ?? GetProfile(session.Summary.ScenarioId ?? string.Empty);
        var availability = profile.Mode == FakeAdapterScenarioMode.Unsupported
            ? AdapterResourceAvailability.Unsupported
            : AdapterResourceAvailability.Available;

        return Results.Json(new AdapterMetricsResponse
        {
            Session = session.Summary,
            Availability = availability,
            CapturedAt = DateTimeOffset.UtcNow,
            Metrics = availability == AdapterResourceAvailability.Available ? profile.Metrics : [],
            Notes = availability == AdapterResourceAvailability.Unsupported && profile.Reason is not null ? [profile.Reason] : [],
            Operation = CreateOperation(availability == AdapterResourceAvailability.Unsupported
                ? AdapterOperationResultCategory.Unsupported
                : AdapterOperationResultCategory.Succeeded, availability == AdapterResourceAvailability.Unsupported ? profile.Reason : "Metrics snapshot captured.")
        }, ProtocolLabAdapterJson.Options);
    }

    private IResult GetArtifactsAsync(string sessionId)
    {
        MaybeDelay();
        if (!TryGetSession(sessionId, out var session))
        {
            return Problem(sessionId, "session-not-found", StatusCodes.Status404NotFound, "Unknown session.");
        }

        var profile = session.Profile ?? GetProfile(session.Summary.ScenarioId ?? string.Empty);
        var availability = profile.Mode == FakeAdapterScenarioMode.Unsupported
            ? AdapterResourceAvailability.Unsupported
            : AdapterResourceAvailability.Available;

        return Results.Json(new AdapterArtifactsResponse
        {
            Session = session.Summary,
            Availability = availability,
            Artifacts = availability == AdapterResourceAvailability.Available ? profile.Artifacts : [],
            Operation = CreateOperation(availability == AdapterResourceAvailability.Unsupported
                ? AdapterOperationResultCategory.Unsupported
                : AdapterOperationResultCategory.Succeeded, availability == AdapterResourceAvailability.Unsupported ? profile.Reason : "Artifacts discovered.")
        }, ProtocolLabAdapterJson.Options);
    }

    private IResult StopSessionAsync(string sessionId)
    {
        MaybeDelay();
        if (!TryGetSession(sessionId, out var session))
        {
            return Problem(sessionId, "session-not-found", StatusCodes.Status404NotFound, "Unknown session.");
        }

        var profile = session.Profile ?? GetProfile(session.Summary.ScenarioId ?? string.Empty);
        session.Summary = session.Summary with
        {
            State = profile.Mode == FakeAdapterScenarioMode.Unsupported
                ? AdapterSessionState.Unsupported
                : AdapterSessionState.Stopped,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        session.LastOperation = CreateOperation(profile.Mode == FakeAdapterScenarioMode.Unsupported
            ? AdapterOperationResultCategory.Unsupported
            : AdapterOperationResultCategory.Succeeded, profile.Mode == FakeAdapterScenarioMode.Unsupported ? profile.Reason : "Session stopped.");

        return Results.Json(session.LastOperation, ProtocolLabAdapterJson.Options);
    }

    private IResult DeleteSessionAsync(string sessionId)
    {
        MaybeDelay();
        lock (_gate)
        {
            if (_deletedSessions.Contains(sessionId))
            {
                return Results.Json(new AdapterSessionResource
                {
                    Session = new AdapterSessionSummary
                    {
                        SessionId = sessionId,
                        State = AdapterSessionState.Disposed,
                        UpdatedAt = DateTimeOffset.UtcNow
                    },
                    Operation = CreateOperation(AdapterOperationResultCategory.Succeeded, "Session deleted.")
                }, ProtocolLabAdapterJson.Options);
            }

            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Problem(sessionId, "session-not-found", StatusCodes.Status404NotFound, "Unknown session.");
            }

            session.Summary = session.Summary with
            {
                State = AdapterSessionState.Disposed,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            session.LastOperation = CreateOperation(AdapterOperationResultCategory.Succeeded, "Session deleted.");
            _deletedSessions.Add(sessionId);

            return Results.Json(new AdapterSessionResource
            {
                Session = session.Summary,
                Operation = session.LastOperation
            }, ProtocolLabAdapterJson.Options);
        }
    }

    private FakeAdapterScenarioProfile GetProfile(string scenarioId)
    {
        if (_options.ScenarioProfiles.TryGetValue(scenarioId, out var profile))
        {
            return profile;
        }

        return FakeAdapterScenarioProfile.Success(scenarioId);
    }

    private AdapterManifestResponse CreateManifest()
    {
        return new AdapterManifestResponse
        {
            AdapterIdentity = CreateAdapterIdentity(),
            ImplementationIdentity = new AdapterIdentity
            {
                Id = _options.ImplementationId,
                Name = _options.ImplementationName,
                Version = "fixture-1.0",
                Image = "fixture/implementation:local"
            },
            VersionCompatibility = CreateCompatibility(),
            SupportedRoles = _options.SupportedRoles,
            ClaimedCapabilities =
            [
                CreateCapability("session-lifecycle"),
                CreateCapability("scenario-introspection"),
                CreateCapability("endpoint-discovery"),
                CreateCapability("metrics-snapshot"),
                CreateCapability("artifact-discovery")
            ],
            SupportedScenarioSelectors =
            [
                new AdapterScenarioSelector
                {
                    SelectorType = "scenario-id",
                    Expression = "success|unsupported|prepare-failure|start-failure|readiness-failure|metrics|artifacts|cleanup|fixture.adapter.success|fixture.adapter.unsupported|fixture.adapter.prepare-failure|fixture.adapter.start-failure|fixture.adapter.readiness-failure|fixture.adapter.metrics|fixture.adapter.artifacts|fixture.adapter.cleanup|fixture.adapter.quic-discovery|fixture.quic.*",
                    Description = "Deterministic fixture scenarios."
                }
            ],
            SupportedEndpointTypes =
            [
                new AdapterEndpointType
                {
                    Type = "http",
                    Description = "Fake HTTP endpoint.",
                    Protocols = ["h1"]
                },
                new AdapterEndpointType
                {
                    Type = "quic",
                    Description = "Fake UDP/QUIC endpoint.",
                    Protocols = ["quic", "http3"]
                }
            ],
            SupportedArtifactTypes =
            [
                new AdapterArtifactType
                {
                    Type = "log",
                    Description = "Fake adapter log artifact."
                },
                new AdapterArtifactType
                {
                    Type = "metrics",
                    Description = "Fake adapter metrics artifact."
                }
            ],
            MetricsAvailability = new AdapterMetricsAvailability
            {
                Available = true,
                SessionMetricsAvailable = true,
                EndpointMetricsAvailable = true,
                ProcessMetricsAvailable = false,
                ContainerMetricsAvailable = false,
                AvailableKinds = ["snapshot"]
            },
            DefaultResponseContentTypes = ["application/json"]
        };
    }

    private AdapterIdentity CreateAdapterIdentity()
    {
        return new AdapterIdentity
        {
            Id = _options.AdapterId,
            Name = _options.AdapterName,
            Version = "fixture-1.0",
            Vendor = "Incursa Fixture Lab"
        };
    }

    private AdapterVersionCompatibility CreateCompatibility()
    {
        return new AdapterVersionCompatibility
        {
            ContractVersion = _options.ContractVersion,
            CompatibleContractVersions = [_options.ContractVersion]
        };
    }

    private static AdapterCapability CreateCapability(string id)
    {
        return new AdapterCapability
        {
            Id = id,
            Status = AdapterCapabilityStatus.Supported
        };
    }

    private static AdapterOperationResult CreateOperation(AdapterOperationResultCategory category, string? message = null, bool? retryable = null)
    {
        return new AdapterOperationResult
        {
            Category = category,
            Message = message,
            Retryable = retryable
        };
    }

    private static AdapterProblemDetails CreateProblem(string code, int status, string title, string operation, string? detail = null, string? sessionId = null)
    {
        return new AdapterProblemDetails
        {
            Type = "https://incursa.example/problems/fixture-adapter",
            Title = title,
            Status = status,
            Detail = detail,
            Code = code,
            Operation = operation,
            SessionId = sessionId,
            Retryable = false
        };
    }

    private static IResult Problem(string sessionId, string code, int status, string detail)
    {
        return Results.Json(CreateProblem(code, status, detail, "session", detail, sessionId), ProtocolLabAdapterJson.Options, statusCode: status, contentType: "application/problem+json");
    }

    private bool TryGetSession(string sessionId, out FakeAdapterSessionState session)
    {
        lock (_gate)
        {
            if (_deletedSessions.Contains(sessionId))
            {
                session = null!;
                return false;
            }

            return _sessions.TryGetValue(sessionId, out session!);
        }
    }

    private void MaybeDelay()
    {
        if (_options.ResponseDelay <= TimeSpan.Zero)
        {
            return;
        }

        Task.Delay(_options.ResponseDelay).GetAwaiter().GetResult();
    }

    private static string GetControlPlaneBaseAddress(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("The fake adapter host did not expose a control plane address.");
        }

        return address;
    }
}

internal sealed class FakeAdapterSessionState
{
    public FakeAdapterSessionState(string sessionId)
    {
        SessionId = sessionId;
        Summary = new AdapterSessionSummary
        {
            SessionId = sessionId,
            State = AdapterSessionState.Created,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public string SessionId { get; }

    public AdapterSessionSummary Summary { get; set; }

    public FakeAdapterScenarioProfile? Profile { get; set; }

    public AdapterOperationResult? LastOperation { get; set; }
}

internal static class FakeAdapterData
{
    public static AdapterEndpoint CreateQuicEndpoint()
    {
        return new AdapterEndpoint
        {
            EndpointId = "endpoint-001",
            Purpose = "test",
            Scheme = "quic",
            Protocol = "quic",
            Host = "127.0.0.1",
            Port = 4433,
            NetworkMode = "process-local",
            BindMode = "loopback",
            Tls = new AdapterTlsNotes
            {
                CertificateMode = "fixture-self-signed",
                CertificateNotes = "Fake QUIC endpoint uses a deterministic self-signed certificate note.",
                Sni = "fixture-quic"
            },
            Extensions = new Dictionary<string, JsonElement>
            {
                ["alpn"] = ProtocolLabAdapterJson.SerializeValue((IReadOnlyList<string>)new[] { "quic" }),
                ["sni"] = ProtocolLabAdapterJson.SerializeValue("fixture-quic"),
                ["streamBehavior"] = ProtocolLabAdapterJson.SerializeValue("bidirectional"),
                ["supportedStreamDirections"] = ProtocolLabAdapterJson.SerializeValue((IReadOnlyList<string>)new[] { "bidirectional", "unidirectional" }),
                ["datagramSupported"] = ProtocolLabAdapterJson.SerializeValue(false),
                ["zeroRttSupported"] = ProtocolLabAdapterJson.SerializeValue(false),
                ["transport"] = ProtocolLabAdapterJson.SerializeValue("udp")
            }
        };
    }

    public static AdapterMetric CreateMetric(string metricId, string scope, int value)
    {
        return new AdapterMetric
        {
            MetricId = metricId,
            Scope = scope,
            Value = JsonSerializer.SerializeToElement(value, ProtocolLabAdapterJson.Options)
        };
    }

    public static AdapterArtifact CreateArtifact(string artifactId, string artifactType, string path)
    {
        return new AdapterArtifact
        {
            ArtifactId = artifactId,
            ArtifactType = artifactType,
            Path = path,
            ContentType = "text/plain",
            Final = true
        };
    }
}
