// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.ProtocolLab.Adapters.MsQuicDotNet;
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

static int ParseInt(string? value, int defaultValue)
{
    return int.TryParse(
        value,
        System.Globalization.NumberStyles.Integer,
        System.Globalization.CultureInfo.InvariantCulture,
        out var parsed)
        ? parsed
        : defaultValue;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new MsQuicDotNetAdapterRuntime(new MsQuicDotNetAdapterOptions
{
    ControlPlaneBaseUrl = builder.Configuration["ASPNETCORE_URLS"] ?? string.Empty,
    QuicPort = ParseInt(builder.Configuration["PROTOCOL_LAB_MSQUIC_PORT"], 0),
    QuicAlpn = builder.Configuration["PROTOCOL_LAB_MSQUIC_ALPN"] ?? "plab-raw-quic",
    CertificateSubject = builder.Configuration["PROTOCOL_LAB_MSQUIC_CERT_SUBJECT"] ?? "CN=ProtocolLab-MSQuic-Local",
    StartTimeout = TimeSpan.FromSeconds(ParseDouble(builder.Configuration["PROTOCOL_LAB_MSQUIC_START_TIMEOUT_SECONDS"], 10)),
    ReadinessTimeout = TimeSpan.FromSeconds(ParseDouble(builder.Configuration["PROTOCOL_LAB_MSQUIC_READINESS_TIMEOUT_SECONDS"], 10)),
    HttpTimeout = TimeSpan.FromSeconds(ParseDouble(builder.Configuration["PROTOCOL_LAB_MSQUIC_HTTP_TIMEOUT_SECONDS"], 5))
}));

var app = builder.Build();
var group = app.MapGroup(AdapterRoutes.Prefix);

group.MapGet("/health", async (MsQuicDotNetAdapterRuntime runtime) =>
{
    var response = await runtime.GetHealthAsync();
    return Results.Json(response, ProtocolLabAdapterJson.Options);
});

group.MapGet("/manifest", async (MsQuicDotNetAdapterRuntime runtime) =>
{
    var response = await runtime.GetManifestAsync();
    return Results.Json(response, ProtocolLabAdapterJson.Options);
});

group.MapPost("/sessions", async (MsQuicDotNetAdapterRuntime runtime, AdapterSessionCreateRequest request) =>
{
    try
    {
        var response = await runtime.CreateSessionAsync(request);
        return Results.Json(response, ProtocolLabAdapterJson.Options, statusCode: StatusCodes.Status201Created);
    }
    catch (MsQuicDotNetAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}", async (MsQuicDotNetAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetSessionAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (MsQuicDotNetAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapPost("/sessions/{sessionId}/prepare", async (MsQuicDotNetAdapterRuntime runtime, string sessionId, AdapterPrepareRequest request) =>
{
    try
    {
        var response = await runtime.PrepareAsync(sessionId, request);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (MsQuicDotNetAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapPost("/sessions/{sessionId}/start", async (MsQuicDotNetAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.StartAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (MsQuicDotNetAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/status", async (MsQuicDotNetAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetStatusAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (MsQuicDotNetAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/endpoints", async (MsQuicDotNetAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetEndpointsAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (MsQuicDotNetAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/metrics", async (MsQuicDotNetAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetMetricsAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (MsQuicDotNetAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapGet("/sessions/{sessionId}/artifacts", async (MsQuicDotNetAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.GetArtifactsAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (MsQuicDotNetAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapPost("/sessions/{sessionId}/stop", async (MsQuicDotNetAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.StopAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (MsQuicDotNetAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

group.MapDelete("/sessions/{sessionId}", async (MsQuicDotNetAdapterRuntime runtime, string sessionId) =>
{
    try
    {
        var response = await runtime.DeleteSessionAsync(sessionId);
        return Results.Json(response, ProtocolLabAdapterJson.Options);
    }
    catch (MsQuicDotNetAdapterProblemException exception)
    {
        return Problem(exception);
    }
});

app.Run();

static IResult Problem(MsQuicDotNetAdapterProblemException exception)
{
    return Results.Json(
        exception.Problem,
        ProtocolLabAdapterJson.Options,
        contentType: "application/problem+json",
        statusCode: (int)exception.StatusCode);
}

public partial class Program;
