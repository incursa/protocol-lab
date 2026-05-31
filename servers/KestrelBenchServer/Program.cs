// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
ConfigureProtocolLabEndpoints(builder);
var app = builder.Build();

app.MapGet("/plaintext", () => TypedResults.Text("Hello, World!", "text/plain"));

app.MapGet("/json", () => TypedResults.Json(new JsonMessage("Hello, World!")));

app.MapGet("/status", (HttpContext context) => TypedResults.Json(new StatusResponse(
    "KestrelBenchServer",
    Environment.GetEnvironmentVariable("PROTOCOL_LAB_IMPLEMENTATION") ?? "kestrel-http3",
    context.Request.Protocol,
    DateTimeOffset.UtcNow,
    Environment.ProcessId)));

app.MapGet("/bytes/{size:int}", (int size) =>
{
    if (size < 0)
    {
        return Results.BadRequest();
    }

    return Results.Bytes(CreateDeterministicBytes(size), "application/octet-stream");
});

app.MapGet("/stream/bytes", async (HttpContext context, int chunks, int size, int delayMs) =>
{
    if (chunks < 0 || size < 0 || delayMs < 0)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    context.Response.ContentType = "application/octet-stream";
    var chunk = CreateDeterministicBytes(size);

    try
    {
        for (var index = 0; index < chunks; index++)
        {
            await context.Response.Body.WriteAsync(chunk, context.RequestAborted);
            if (delayMs > 0 && index + 1 < chunks)
            {
                await Task.Delay(delayMs, context.RequestAborted);
            }
        }
    }
    catch (OperationCanceledException)
    {
    }
    catch (IOException)
    {
    }
});

app.MapPost("/sink", async (HttpRequest request) =>
{
    var body = await ReadRequestBodyAsync(request);
    return TypedResults.Json(new BytesReadResponse(body.Length));
});

app.MapPost("/hash", async (HttpRequest request) =>
{
    var body = await ReadRequestBodyAsync(request);
    var hash = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
    return TypedResults.Json(new HashResponse(body.Length, hash));
});

app.MapPost("/echo", async (HttpRequest request) =>
{
    var body = await ReadRequestBodyAsync(request);
    return Results.Bytes(body, "application/octet-stream");
});

app.MapGet("/headers/response", (HttpContext context, int count, int size) =>
{
    if (count < 0 || size < 0)
    {
        return Results.BadRequest();
    }

    foreach (var header in CreateSyntheticHeaders(count, size))
    {
        context.Response.Headers[header.Key] = header.Value;
    }

    return Results.Text("headers", "text/plain");
});

app.MapGet("/inspect/headers", (HttpRequest request) =>
{
    var headers = request.Headers.ToDictionary(
        header => header.Key,
        header => string.Join(",", header.Value.ToArray()),
        StringComparer.OrdinalIgnoreCase);
    return TypedResults.Json(headers);
});

app.Run();

static async Task<byte[]> ReadRequestBodyAsync(HttpRequest request)
{
    using var memory = new MemoryStream();
    await request.Body.CopyToAsync(memory);
    return memory.ToArray();
}

static Dictionary<string, string> CreateSyntheticHeaders(int count, int size)
{
    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var value = new string('a', size);

    for (var index = 0; index < count; index++)
    {
        headers[$"X-Protocol-Bench-Header-{index:000}"] = value;
    }

    return headers;
}

static byte[] CreateDeterministicBytes(int size)
{
    var bytes = new byte[size];
    for (var index = 0; index < bytes.Length; index++)
    {
        bytes[index] = (byte)(index % 251);
    }

    return bytes;
}

static void ConfigureProtocolLabEndpoints(WebApplicationBuilder builder)
{
    if (!IsEnabled(Environment.GetEnvironmentVariable("PROTOCOL_LAB_ENABLE_EXPLICIT_ENDPOINTS")))
    {
        return;
    }

    var h1Url = Environment.GetEnvironmentVariable("PROTOCOL_LAB_H1_URL") ?? "http://127.0.0.1:5080";
    var httpsUrl = Environment.GetEnvironmentVariable("PROTOCOL_LAB_HTTPS_URL")
        ?? Environment.GetEnvironmentVariable("PROTOCOL_LAB_H3_URL")
        ?? "https://127.0.0.1:5443";
    var endpointKinds = ParseEndpointKinds(Environment.GetEnvironmentVariable("PROTOCOL_LAB_ENDPOINTS"));
    var enableHttp = endpointKinds.Count == 0 || endpointKinds.Contains("http") || endpointKinds.Contains("h1");
    var enableHttps = endpointKinds.Count == 0 ||
        endpointKinds.Contains("https") ||
        endpointKinds.Contains("h2") ||
        endpointKinds.Contains("h3");
    var httpsProtocols = ParseHttpsProtocols(Environment.GetEnvironmentVariable("PROTOCOL_LAB_HTTPS_PROTOCOLS"));

    builder.WebHost.ConfigureKestrel(options =>
    {
        if (enableHttp)
        {
            ConfigureEndpoint(options, h1Url, HttpProtocols.Http1);
        }

        if (enableHttps)
        {
            ConfigureEndpoint(options, httpsUrl, httpsProtocols, requireHttps: true);
        }
    });
}

static HashSet<string> ParseEndpointKinds(string? value)
{
    var kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(value))
    {
        return kinds;
    }

    foreach (var part in value.Split([',', ';', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        kinds.Add(part);
    }

    return kinds;
}

static HttpProtocols ParseHttpsProtocols(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return HttpProtocols.Http1AndHttp2AndHttp3;
    }

    var normalized = value.Trim();
    if (string.Equals(normalized, "h1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(normalized, "http1", StringComparison.OrdinalIgnoreCase))
    {
        return HttpProtocols.Http1;
    }

    if (string.Equals(normalized, "h2", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(normalized, "http2", StringComparison.OrdinalIgnoreCase))
    {
        return HttpProtocols.Http2;
    }

    if (string.Equals(normalized, "h3", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(normalized, "http3", StringComparison.OrdinalIgnoreCase))
    {
        return HttpProtocols.Http1AndHttp2AndHttp3;
    }

    if (string.Equals(normalized, "http1andhttp2", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(normalized, "h1andh2", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(normalized, "h1+h2", StringComparison.OrdinalIgnoreCase))
    {
        return HttpProtocols.Http1AndHttp2;
    }

    return HttpProtocols.Http1AndHttp2AndHttp3;
}

static void ConfigureEndpoint(KestrelServerOptions options, string url, HttpProtocols protocols, bool requireHttps = false)
{
    var uri = new Uri(url);
    var address = ResolveListenAddress(uri.Host);
    var port = uri.Port;

    options.Listen(address, port, listen =>
    {
        listen.Protocols = protocols;
        if (requireHttps || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            var certificatePath = Environment.GetEnvironmentVariable("PROTOCOL_LAB_CERTIFICATE_PATH");
            if (string.IsNullOrWhiteSpace(certificatePath))
            {
                if (IsEnabled(Environment.GetEnvironmentVariable("PROTOCOL_LAB_GENERATE_LOCAL_CERT")))
                {
                    listen.UseHttps(CreateLocalDevelopmentCertificate());
                }
                else
                {
                    listen.UseHttps();
                }
            }
            else
            {
                listen.UseHttps(X509CertificateLoader.LoadPkcs12FromFile(
                    certificatePath,
                    Environment.GetEnvironmentVariable("PROTOCOL_LAB_CERTIFICATE_PASSWORD")));
            }
        }
    });
}

static IPAddress ResolveListenAddress(string host)
{
    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
    {
        return IPAddress.Loopback;
    }

    return IPAddress.TryParse(host, out var address)
        ? address
        : IPAddress.Loopback;
}

static X509Certificate2 CreateLocalDevelopmentCertificate()
{
    using var key = RSA.Create(2048);
    var request = new CertificateRequest(
        "CN=localhost",
        key,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
    request.CertificateExtensions.Add(new X509KeyUsageExtension(
        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
        false));
    request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

    var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
    subjectAlternativeNames.AddDnsName("localhost");
    subjectAlternativeNames.AddDnsName("host.docker.internal");
    subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
    subjectAlternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
    request.CertificateExtensions.Add(subjectAlternativeNames.Build());

    using var certificate = request.CreateSelfSigned(
        DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddDays(7));
    return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), password: null);
}

static bool IsEnabled(string? value)
{
    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}

internal sealed record JsonMessage([property: JsonPropertyName("message")] string Message);

internal sealed record StatusResponse(
    [property: JsonPropertyName("server")] string Server,
    [property: JsonPropertyName("implementation")] string Implementation,
    [property: JsonPropertyName("protocol")] string Protocol,
    [property: JsonPropertyName("utc")] DateTimeOffset Utc,
    [property: JsonPropertyName("processId")] int ProcessId);

internal sealed record BytesReadResponse([property: JsonPropertyName("bytesRead")] int BytesRead);

internal sealed record HashResponse(
    [property: JsonPropertyName("bytesRead")] int BytesRead,
    [property: JsonPropertyName("sha256")] string Sha256);
