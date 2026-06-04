// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Incursa.Qpack;
using Incursa.ProtocolLab.Adapter.Contracts;
using Incursa.Quic;
using Incursa.Quic.Http3;

namespace Incursa.ProtocolLab.Adapters.IncursaHttp3;

internal sealed record IncursaHttp3EndpointOptions
{
    public int Port { get; init; }

    public string ImplementationId { get; init; } = "incursa-http3";

    public string ImplementationName { get; init; } = "Incursa HTTP/3";

    public string? SessionId { get; init; }

    public string Mode { get; init; } = "endpoint";

    public string ReadinessProbePath { get; init; } = "/plaintext";

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}

internal sealed class IncursaHttp3EndpointHost : IAsyncDisposable
{
    private readonly Http3Server server;
    private readonly Task serveTask;
    private readonly CancellationTokenSource shutdown = new();
    private readonly X509Certificate2 certificate;

    private int disposed;

    private IncursaHttp3EndpointHost(
        Http3Server server,
        Task serveTask,
        X509Certificate2 certificate,
        AdapterEndpoint endpoint,
        string commandLine,
        IncursaHttp3EndpointMetricsSink metricsSink)
    {
        this.server = server;
        this.serveTask = serveTask;
        this.certificate = certificate;
        Endpoint = endpoint;
        CommandLine = commandLine;
        MetricsSink = metricsSink;
    }

    public AdapterEndpoint Endpoint { get; }

    public string CommandLine { get; }

    public IncursaHttp3EndpointMetricsSink MetricsSink { get; }

    public static async ValueTask<IncursaHttp3EndpointHost> StartAsync(
        IncursaHttp3EndpointOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (!QuicListener.IsSupported)
        {
            throw new PlatformNotSupportedException("Incursa QUIC listener support is not available on this machine.");
        }

        var certificate = CreateLoopbackCertificate();
        var metricsSink = new IncursaHttp3EndpointMetricsSink();
        var listenerOptions = CreateListenerOptions(options.Port, certificate);
        var serverOptions = new Http3ServerOptions
        {
            DiagnosticsSink = metricsSink
        };

        var server = await Http3Server.ListenAsync(
            listenerOptions,
            new IncursaHttp3RequestHandler(options, metricsSink),
            serverOptions,
            cancellationToken).ConfigureAwait(false);

        var endpoint = CreateEndpoint(options.Port, options.SessionId);
        var commandLine = BuildCommandLine(options);
        var serveTask = server.ServeAsync(CancellationToken.None);
        var host = new IncursaHttp3EndpointHost(
            server,
            serveTask,
            certificate,
            endpoint,
            commandLine,
            metricsSink);

        return host;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        shutdown.Cancel();

        try
        {
            await server.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        try
        {
            await serveTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch
        {
        }

        certificate.Dispose();
        shutdown.Dispose();
    }

    private static QuicListenerOptions CreateListenerOptions(int port, X509Certificate2 certificate)
    {
        return new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(IPAddress.IPv6Any, port),
            ApplicationProtocols = [SslApplicationProtocol.Http3],
            ListenBacklog = 512,
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                MaxInboundBidirectionalStreams = 512,
                MaxInboundUnidirectionalStreams = 16,
                InitialReceiveWindowSizes = new QuicReceiveWindowSizes
                {
                    Connection = 16 * 1024 * 1024,
                    LocallyInitiatedBidirectionalStream = 16 * 1024 * 1024,
                    RemotelyInitiatedBidirectionalStream = 16 * 1024 * 1024,
                    UnidirectionalStream = 16 * 1024 * 1024,
                },
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = [SslApplicationProtocol.Http3],
                    EnabledSslProtocols = SslProtocols.Tls13,
                    EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                    ServerCertificate = certificate,
                },
            }),
        };
    }

    private static AdapterEndpoint CreateEndpoint(int port, string? sessionId)
    {
        return new AdapterEndpoint
        {
            EndpointId = string.IsNullOrWhiteSpace(sessionId)
                ? $"incursa-http3-endpoint-{port}"
                : $"incursa-http3-endpoint-{sessionId}",
            Purpose = "server",
            Scheme = "https",
            Protocol = "h3",
            Host = "127.0.0.1",
            Port = port,
            Path = "/",
            Authority = $"127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}",
            SocketAddress = $"127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}",
            NetworkMode = "process-local",
            BindMode = "loopback",
            Tls = new AdapterTlsNotes
            {
                CertificateMode = "loopback-self-signed-certificate",
                CertificateNotes = "The endpoint uses a runtime-generated self-signed certificate for loopback-only HTTP/3 sessions.",
                Sni = "localhost",
                VerificationNotes = "ProtocolLab bypasses loopback certificate validation for local proof only."
            },
            Metadata =
            {
                ["transport"] = JsonSerializer.SerializeToElement("udp", ProtocolLabAdapterJson.Options),
                ["alpn"] = JsonSerializer.SerializeToElement("h3", ProtocolLabAdapterJson.Options),
                ["mode"] = JsonSerializer.SerializeToElement("endpoint", ProtocolLabAdapterJson.Options)
            }
        };
    }

    private static string BuildCommandLine(IncursaHttp3EndpointOptions options)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"dotnet run --project src/Incursa.ProtocolLab.Adapters.IncursaHttp3/Incursa.ProtocolLab.Adapters.IncursaHttp3.csproj -- --mode {options.Mode} --port {options.Port}");
    }

    private static X509Certificate2 CreateLoopbackCertificate()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new("CN=localhost", ecdsa, HashAlgorithmName.SHA256);
        SubjectAlternativeNameBuilder subjectAlternativeNames = new();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30));
        byte[] pfxBytes = certificate.Export(X509ContentType.Pkcs12);
        return X509CertificateLoader.LoadPkcs12(
            pfxBytes,
            (string?)null,
            X509KeyStorageFlags.Exportable);
    }
}

internal sealed class IncursaHttp3EndpointMetricsSink : IHttp3DiagnosticsSink
{
    private int activeConnections;
    private int activeRequests;
    private static readonly bool DebugLogging = string.Equals(
        Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_HTTP3_DEBUG"),
        "1",
        StringComparison.Ordinal);

    public bool IsEnabled => true;

    public int ActiveConnections => Volatile.Read(ref activeConnections);

    public int ActiveRequests => Volatile.Read(ref activeRequests);

    public void Emit(Http3DiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);

        if (DebugLogging)
        {
            switch (diagnosticEvent.Kind)
            {
                case Http3DiagnosticKind.ConnectionStarted:
                case Http3DiagnosticKind.ConnectionClosed:
                case Http3DiagnosticKind.FrameReceived:
                case Http3DiagnosticKind.RequestStarted:
                case Http3DiagnosticKind.RequestCompleted:
                case Http3DiagnosticKind.ResponseStarted:
                case Http3DiagnosticKind.ResponseCompleted:
                    Console.Error.WriteLine(
                        $"IncursaH3 diag {diagnosticEvent.Kind} role={diagnosticEvent.Role ?? "<none>"} stream={diagnosticEvent.StreamId?.ToString(CultureInfo.InvariantCulture) ?? "<none>"} frame={diagnosticEvent.FrameType?.ToString() ?? diagnosticEvent.RawFrameType?.ToString(CultureInfo.InvariantCulture) ?? "<none>"} payload={diagnosticEvent.PayloadLength?.ToString(CultureInfo.InvariantCulture) ?? "<none>"} method={diagnosticEvent.Method ?? "<none>"} path={diagnosticEvent.Path ?? "<none>"} status={diagnosticEvent.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}");
                    break;
            }
        }

        switch (diagnosticEvent.Kind)
        {
            case Http3DiagnosticKind.ConnectionStarted:
                Interlocked.Increment(ref activeConnections);
                break;
            case Http3DiagnosticKind.ConnectionClosed:
                Interlocked.Decrement(ref activeConnections);
                break;
            case Http3DiagnosticKind.RequestStarted:
                Interlocked.Increment(ref activeRequests);
                break;
            case Http3DiagnosticKind.RequestCompleted:
                Interlocked.Decrement(ref activeRequests);
                break;
        }
    }
}

internal sealed class IncursaHttp3RequestHandler : IHttp3RequestHandler
{
    private const int StatusOk = 200;
    private const int StatusBadRequest = 400;
    private const int StatusNotFound = 404;
    private const int StatusMethodNotAllowed = 405;
    private const int StatusPayloadTooLarge = 413;
    private const int StatusInternalServerError = 500;
    private static readonly bool DebugLogging = string.Equals(
        Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_HTTP3_DEBUG"),
        "1",
        StringComparison.Ordinal);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IncursaHttp3EndpointOptions options;

    public IncursaHttp3RequestHandler(
        IncursaHttp3EndpointOptions options,
        IncursaHttp3EndpointMetricsSink metricsSink)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        _ = metricsSink;
    }

    public ValueTask<Http3ServerResponse> HandleAsync(Http3Request request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (DebugLogging)
        {
            Console.Error.WriteLine($"IncursaH3 handling {request.Method} {request.Path} bodyLength={request.Body.Length}");
        }

        var target = RequestTarget.Parse(request.Path);
        Http3ServerResponse response = request.Method switch
        {
            "GET" => HandleGet(request, target, cancellationToken),
            "POST" => HandlePost(request, target),
            _ => Text(StatusMethodNotAllowed, "Method Not Allowed"),
        };

        if (DebugLogging)
        {
            Console.Error.WriteLine($"IncursaH3 completed {request.Method} {request.Path} status={response.StatusCode} bodyLength={response.Body.Length}");
        }

        return ValueTask.FromResult(response);
    }

    private Http3ServerResponse HandleGet(Http3Request request, RequestTarget target, CancellationToken cancellationToken)
    {
        if (target.Path == "/plaintext")
        {
            return Binary(StatusOk, HelloWorld.Plaintext, "text/plain; charset=utf-8");
        }

        if (target.Path == "/json")
        {
            return Binary(StatusOk, HelloWorld.Json, "application/json");
        }

        if (target.Path == "/status")
        {
            return Json(StatusOk, CreateStatusPayload());
        }

        if (target.Path.StartsWith("/bytes/", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(target.Path["/bytes/".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var byteSize) &&
            byteSize >= 0)
        {
            return Binary(StatusOk, CreateDeterministicBytes(byteSize), "application/octet-stream");
        }

        if (target.Path == "/stream/bytes")
        {
            var chunks = ParsePositiveInt(target.Query, "chunks", 100);
            var chunkSize = ParsePositiveInt(target.Query, "size", 16 * 1024);
            var delayMs = ParsePositiveInt(target.Query, "delayMs", 0);
            return Http3ServerResponse.CreateStreaming(
                StatusOk,
                StreamDeterministicBytes(chunks, chunkSize, delayMs, cancellationToken),
                [new QPackFieldLine("content-type", "application/octet-stream")]);
        }

        if (target.Path == "/headers/response")
        {
            var count = ParsePositiveInt(target.Query, "count", 50);
            var headerSize = ParsePositiveInt(target.Query, "size", 32);
            return Text(
                StatusOk,
                "headers",
                "text/plain; charset=utf-8",
                CreateSyntheticHeaders(count, headerSize));
        }

        if (target.Path == "/inspect/headers")
        {
            return Json(StatusOk, CreateInspectHeadersPayload(request, target));
        }

        return Text(StatusNotFound, "Not Found");
    }

    private Http3ServerResponse HandlePost(Http3Request request, RequestTarget target)
    {
        if (target.Path == "/echo")
        {
            return Binary(StatusOk, request.Body, "application/octet-stream");
        }

        if (target.Path == "/hash")
        {
            return Json(StatusOk, new
            {
                bytesRead = request.Body.Length,
                sha256 = Convert.ToHexString(SHA256.HashData(request.Body.Span)).ToLowerInvariant(),
            });
        }

        if (target.Path == "/sink")
        {
            var bytesRead = CountBytes(request.Body.Span);
            return Json(StatusOk, new
            {
                bytesRead,
            });
        }

        if (target.Path == "/upload")
        {
            return Json(StatusOk, new
            {
                bytesRead = request.Body.Length,
            });
        }

        return Text(StatusNotFound, "Not Found");
    }

    private static int CountBytes(ReadOnlySpan<byte> body)
    {
        var count = 0;
        foreach (var _ in body)
        {
            count++;
        }

        return count;
    }

    private object CreateStatusPayload()
    {
        return new
        {
            server = "KestrelBenchServer",
            implementation = "kestrel-http3",
            protocol = "h3",
            utc = options.TimeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
            processId = Environment.ProcessId,
            adapterServer = "Incursa HTTP/3",
            adapterImplementation = options.ImplementationId,
        };
    }

    private static object CreateInspectHeadersPayload(Http3Request request, RequestTarget target)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["method"] = request.Method,
            ["path"] = target.Path,
            ["queryString"] = target.Query,
        };

        foreach (QPackFieldLine header in request.Headers)
        {
            if (header.Name.StartsWith(":", StringComparison.Ordinal))
            {
                continue;
            }

            payload[CanonicalizeHeaderName(header.Name)] = header.Value;
        }

        return payload;
    }

    private static string CanonicalizeHeaderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length);
        var capitalize = true;

        foreach (var character in name)
        {
            if (character == '-')
            {
                builder.Append('-');
                capitalize = true;
                continue;
            }

            builder.Append(capitalize ? char.ToUpperInvariant(character) : char.ToLowerInvariant(character));
            capitalize = false;
        }

        return builder.ToString();
    }

    private static IReadOnlyList<QPackFieldLine> CreateSyntheticHeaders(int count, int size)
    {
        var headers = new List<QPackFieldLine>(count + 2);
        for (var index = 0; index < count; index++)
        {
            headers.Add(new QPackFieldLine(
                $"x-protocol-bench-header-{index:000}",
                CreateHeaderValue(index, size)));
        }

        headers.Add(new QPackFieldLine("content-type", "text/plain; charset=utf-8"));
        headers.Add(new QPackFieldLine("content-length", HelloWorld.Headers.Length.ToString(CultureInfo.InvariantCulture)));
        return headers;
    }

    private static string CreateHeaderValue(int index, int size)
    {
        if (size <= 0)
        {
            return string.Empty;
        }

        Span<char> buffer = size <= 256 ? stackalloc char[size] : new char[size];
        for (var position = 0; position < size; position++)
        {
            buffer[position] = (char)('a' + ((index + position) % 26));
        }

        return new string(buffer);
    }

    private static Http3ServerResponse Text(
        int statusCode,
        string value,
        string contentType = "text/plain; charset=utf-8",
        IEnumerable<QPackFieldLine>? extraHeaders = null)
    {
        return Binary(statusCode, Encoding.UTF8.GetBytes(value), contentType, extraHeaders);
    }

    private static Http3ServerResponse Json(int statusCode, object value)
    {
        return Binary(statusCode, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions), "application/json");
    }

    private static Http3ServerResponse Binary(
        int statusCode,
        ReadOnlyMemory<byte> body,
        string contentType,
        IEnumerable<QPackFieldLine>? extraHeaders = null)
    {
        var headers = new List<QPackFieldLine>
        {
            new("content-type", contentType),
            new("content-length", body.Length.ToString(CultureInfo.InvariantCulture)),
            new("server", "Incursa.Quic.Http3"),
        };

        if (extraHeaders is not null)
        {
            headers.InsertRange(0, extraHeaders);
        }

        return new Http3ServerResponse(statusCode, body, headers, null, false, false, null);
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamDeterministicBytes(
        int chunks,
        int size,
        int delayMs,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var chunk = 0; chunk < chunks; chunk++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return CreateDeterministicBytes(size, chunk * size);
            if (delayMs > 0 && chunk + 1 < chunks)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static int ParsePositiveInt(string query, string key, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return defaultValue;
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || !parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return int.TryParse(Uri.UnescapeDataString(parts[1]), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
                ? parsed
                : defaultValue;
        }

        return defaultValue;
    }

    private static byte[] CreateDeterministicBytes(int size, int offset = 0)
    {
        var bytes = new byte[size];
        for (var index = 0; index < size; index++)
        {
            bytes[index] = (byte)((offset + index) % 251);
        }

        return bytes;
    }
}

internal sealed record RequestTarget(string Path, string Query)
{
    public static RequestTarget Parse(string requestTarget)
    {
        ArgumentNullException.ThrowIfNull(requestTarget);

        var queryIndex = requestTarget.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0)
        {
            return new RequestTarget(string.IsNullOrWhiteSpace(requestTarget) ? "/" : requestTarget, string.Empty);
        }

        var path = queryIndex == 0 ? "/" : requestTarget[..queryIndex];
        return new RequestTarget(path, requestTarget[(queryIndex + 1)..]);
    }
}

internal static class HelloWorld
{
    public static readonly byte[] Plaintext = "Hello, World!"u8.ToArray();

    public static readonly byte[] Json = """{"message":"Hello, World!"}"""u8.ToArray();

    public const string Headers = "headers";
}
