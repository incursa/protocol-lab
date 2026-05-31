// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Adapters.Kestrel;
using Microsoft.AspNetCore.Http;

static double ParseDouble(string? value, double defaultValue)
{
    return double.TryParse(
        value,
        System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture,
        out var parsed)
        ? parsed
        : defaultValue;
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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new KestrelAdapterRuntime(new KestrelAdapterOptions
{
    RepositoryRoot = ResolveRepositoryRoot(builder.Environment.ContentRootPath),
    ControlPlaneBaseUrl = builder.Configuration["ASPNETCORE_URLS"] ?? string.Empty,
    BenchmarkServerProjectPath = builder.Configuration["PROTOCOL_LAB_KESTREL_BENCHMARK_PROJECT_PATH"] ??
        Path.Combine("servers", "KestrelBenchServer", "KestrelBenchServer.csproj"),
    ReadinessProbePath = builder.Configuration["PROTOCOL_LAB_KESTREL_READINESS_PROBE_PATH"] ?? "/plaintext",
    StartTimeout = TimeSpan.FromSeconds(ParseDouble(builder.Configuration["PROTOCOL_LAB_KESTREL_START_TIMEOUT_SECONDS"], 30)),
    ReadinessTimeout = TimeSpan.FromSeconds(ParseDouble(builder.Configuration["PROTOCOL_LAB_KESTREL_READINESS_TIMEOUT_SECONDS"], 30)),
    HttpTimeout = TimeSpan.FromSeconds(ParseDouble(builder.Configuration["PROTOCOL_LAB_KESTREL_HTTP_TIMEOUT_SECONDS"], 5))
}));

var app = builder.Build();
var group = app.MapGroup(AdapterRoutes.Prefix);

group.MapGet("/health", async (KestrelAdapterRuntime runtime) =>
{
    var response = await runtime.GetHealthAsync();
    return Results.Json(response, ProtocolLabAdapterJson.Options);
});

group.MapGet("/manifest", async (KestrelAdapterRuntime runtime) =>
{
    var response = await runtime.GetManifestAsync();
    return Results.Json(response, ProtocolLabAdapterJson.Options);
});

group.MapPost("/sessions", async (KestrelAdapterRuntime runtime, AdapterSessionCreateRequest request) =>
{
    try
    {
        var response = await runtime.CreateSessionAsync(request);
        return Results.Json(response, ProtocolLabAdapterJson.Options, statusCode: StatusCodes.Status201Created);
    }
    catch (KestrelAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}", async (KestrelAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetSessionAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (KestrelAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapPost("/sessions/{sessionId}/prepare", async (KestrelAdapterRuntime runtime, string sessionId, AdapterPrepareRequest request) =>
{
    try
    {
        var response = await runtime.PrepareAsync(sessionId, request);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (KestrelAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapPost("/sessions/{sessionId}/start", async (KestrelAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.StartAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (KestrelAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/status", async (KestrelAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetStatusAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (KestrelAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/endpoints", async (KestrelAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetEndpointsAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (KestrelAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/metrics", async (KestrelAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetMetricsAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (KestrelAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/artifacts", async (KestrelAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetArtifactsAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (KestrelAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapPost("/sessions/{sessionId}/stop", async (KestrelAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.StopAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (KestrelAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapDelete("/sessions/{sessionId}", async (KestrelAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.DeleteSessionAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (KestrelAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

app.Run();

static IResult Problem(KestrelAdapterProblemException exception)
{
    return Results.Json(
        exception.Problem,
        ProtocolLabAdapterJson.Options,
        contentType: "application/problem+json",
        statusCode: (int)exception.StatusCode);
}

public partial class Program;
