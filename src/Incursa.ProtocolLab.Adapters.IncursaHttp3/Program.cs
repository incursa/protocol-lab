// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Globalization;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Adapters.IncursaHttp3;
using Microsoft.AspNetCore.Http;

static string? ReadOption(string[] args, string name)
{
    for (var index = 0; index < args.Length; index++)
    {
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
        {
            return index + 1 < args.Length ? args[index + 1] : null;
        }

        if (args[index].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
        {
            return args[index][(name.Length + 1)..];
        }
    }

    return null;
}

static int ResolvePort(string[] args, string argumentName, string environmentName, int defaultValue)
{
    var rawValue = ReadOption(args, argumentName) ?? Environment.GetEnvironmentVariable(environmentName);
    return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
        ? port
        : defaultValue;
}

static string ResolveMode(string[] args)
{
    return ReadOption(args, "--mode")
        ?? Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_MODE")
        ?? "adapter";
}

static string ResolveRepositoryRoot(string contentRootPath)
{
    var directory = new DirectoryInfo(contentRootPath);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Incursa.ProtocolLab.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return contentRootPath;
}

var mode = ResolveMode(args);
if (string.Equals(mode, "endpoint", StringComparison.OrdinalIgnoreCase))
{
    var endpointPort = ResolvePort(args, "--port", "PROTOCOL_LAB_H3_PORT", 5444);
    var readinessProbePath = Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_READINESS_PROBE_PATH") ?? "/plaintext";

    await using var endpointHost = await IncursaHttp3EndpointHost.StartAsync(new IncursaHttp3EndpointOptions
    {
        Port = endpointPort,
        ImplementationId = "incursa-http3",
        ImplementationName = "Incursa HTTP/3",
        Mode = "endpoint",
        ReadinessProbePath = readinessProbePath,
        TimeProvider = TimeProvider.System
    });

    Console.WriteLine($"Incursa HTTP/3 endpoint listening on https://127.0.0.1:{endpointPort}.");

    using var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        shutdown.Cancel();
    };

    try
    {
        await Task.Delay(Timeout.Infinite, shutdown.Token);
    }
    catch (OperationCanceledException)
    {
    }

    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new IncursaHttp3AdapterRuntime(new IncursaHttp3AdapterOptions
{
    RepositoryRoot = ResolveRepositoryRoot(builder.Environment.ContentRootPath),
    ControlPlaneBaseUrl = builder.Configuration["ASPNETCORE_URLS"] ?? string.Empty,
    ReadinessProbePath = builder.Configuration["PROTOCOL_LAB_INCURSA_READINESS_PROBE_PATH"] ?? "/plaintext",
    ForceEndpointStartFailureMessage = builder.Configuration["PROTOCOL_LAB_INCURSA_FORCE_ENDPOINT_START_FAILURE_MESSAGE"],
    StartTimeout = TimeSpan.FromSeconds(double.TryParse(
        builder.Configuration["PROTOCOL_LAB_INCURSA_START_TIMEOUT_SECONDS"],
        NumberStyles.Float,
        CultureInfo.InvariantCulture,
        out var startTimeoutSeconds)
        ? startTimeoutSeconds
        : 30),
    ReadinessTimeout = TimeSpan.FromSeconds(double.TryParse(
        builder.Configuration["PROTOCOL_LAB_INCURSA_READINESS_TIMEOUT_SECONDS"],
        NumberStyles.Float,
        CultureInfo.InvariantCulture,
        out var readinessTimeoutSeconds)
        ? readinessTimeoutSeconds
        : 30),
    HttpTimeout = TimeSpan.FromSeconds(double.TryParse(
        builder.Configuration["PROTOCOL_LAB_INCURSA_HTTP_TIMEOUT_SECONDS"],
        NumberStyles.Float,
        CultureInfo.InvariantCulture,
        out var httpTimeoutSeconds)
        ? httpTimeoutSeconds
        : 5)
}));

var app = builder.Build();
var group = app.MapGroup(AdapterRoutes.Prefix);

group.MapGet("/health", async (IncursaHttp3AdapterRuntime runtime) =>
{
    var response = await runtime.GetHealthAsync();
    return Results.Json(response, ProtocolLabAdapterJson.Options);
});

group.MapGet("/manifest", async (IncursaHttp3AdapterRuntime runtime) =>
{
    var response = await runtime.GetManifestAsync();
    return Results.Json(response, ProtocolLabAdapterJson.Options);
});

group.MapPost("/sessions", async (IncursaHttp3AdapterRuntime runtime, AdapterSessionCreateRequest request) =>
{
    try
    {
        var response = await runtime.CreateSessionAsync(request);
        return Results.Json(response, ProtocolLabAdapterJson.Options, statusCode: StatusCodes.Status201Created);
    }
    catch (IncursaHttp3AdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}", async (IncursaHttp3AdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetSessionAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (IncursaHttp3AdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapPost("/sessions/{sessionId}/prepare", async (IncursaHttp3AdapterRuntime runtime, string sessionId, AdapterPrepareRequest request) =>
{
    try
    {
        var response = await runtime.PrepareAsync(sessionId, request);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (IncursaHttp3AdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapPost("/sessions/{sessionId}/start", async (IncursaHttp3AdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.StartAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (IncursaHttp3AdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/status", async (IncursaHttp3AdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetStatusAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (IncursaHttp3AdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/endpoints", async (IncursaHttp3AdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetEndpointsAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (IncursaHttp3AdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/metrics", async (IncursaHttp3AdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetMetricsAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (IncursaHttp3AdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/artifacts", async (IncursaHttp3AdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetArtifactsAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (IncursaHttp3AdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapPost("/sessions/{sessionId}/stop", async (IncursaHttp3AdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.StopAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (IncursaHttp3AdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapDelete("/sessions/{sessionId}", async (IncursaHttp3AdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.DeleteSessionAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (IncursaHttp3AdapterProblemException exception)
    {
        return Problem(exception);
    }
});

app.Run();

static IResult Problem(IncursaHttp3AdapterProblemException exception)
{
    return Results.Json(
        exception.Problem,
        ProtocolLabAdapterJson.Options,
        contentType: "application/problem+json",
        statusCode: (int)exception.StatusCode);
}

public partial class Program;
