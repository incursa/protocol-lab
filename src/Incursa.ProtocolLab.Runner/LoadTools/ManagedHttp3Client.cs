// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using Incursa.Qpack;
using Incursa.Quic;
using Incursa.Quic.Http3;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal sealed class ManagedHttp3Client : IAsyncDisposable
{
    private static readonly bool DebugLogging = string.Equals(
        Environment.GetEnvironmentVariable("PROTOCOL_LAB_INCURSA_HTTP3_DEBUG"),
        "1",
        StringComparison.Ordinal);
    private readonly QuicConnection connection;
    private readonly QuicStream controlStream;
    private readonly QuicStream qpackEncoderStream;
    private readonly QuicStream qpackDecoderStream;
    private int disposed;

    private ManagedHttp3Client(
        QuicConnection connection,
        QuicStream controlStream,
        QuicStream qpackEncoderStream,
        QuicStream qpackDecoderStream,
        IReadOnlyList<string> warnings)
    {
        this.connection = connection;
        this.controlStream = controlStream;
        this.qpackEncoderStream = qpackEncoderStream;
        this.qpackDecoderStream = qpackDecoderStream;
        Warnings = warnings;
    }

    public IReadOnlyList<string> Warnings { get; }

    public static async Task<ManagedHttp3Client> ConnectAsync(
        Uri uri,
        bool certificateBypassUsed,
        CancellationToken cancellationToken = default)
    {
        QuicClientConnectionOptions clientOptions = await CreateClientOptionsAsync(uri, certificateBypassUsed).ConfigureAwait(false);
        QuicConnection connection = await QuicConnection.ConnectAsync(clientOptions, cancellationToken).ConfigureAwait(false);
        DebugLog($"managed H3 load connect uri={uri}");

        try
        {
            QuicStream controlStream = await connection
                .OpenOutboundStreamAsync(QuicStreamType.Unidirectional, cancellationToken)
                .ConfigureAwait(false);
            byte[] initialControlStream = Http3SettingsWriter.WriteInitialControlStream(new Http3Settings());
            await controlStream.WriteAsync(initialControlStream, 0, initialControlStream.Length, cancellationToken).ConfigureAwait(false);

            QuicStream qpackEncoderStream = await connection
                .OpenOutboundStreamAsync(QuicStreamType.Unidirectional, cancellationToken)
                .ConfigureAwait(false);
            await WriteStreamTypeAsync(qpackEncoderStream, Http3StreamType.QPackEncoder, cancellationToken).ConfigureAwait(false);

            QuicStream qpackDecoderStream = await connection
                .OpenOutboundStreamAsync(QuicStreamType.Unidirectional, cancellationToken)
                .ConfigureAwait(false);
            await WriteStreamTypeAsync(qpackDecoderStream, Http3StreamType.QPackDecoder, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<string> warnings = certificateBypassUsed
                ? ["Loopback certificate validation bypass was used for managed QUIC HTTP/3 proof."]
                : [];

            DebugLog("managed H3 load client connected");
            return new ManagedHttp3Client(connection, controlStream, qpackEncoderStream, qpackDecoderStream, warnings);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ManagedProofResponse> SendAsync(
        HttpEndpointSpec endpoint,
        Uri uri,
        byte[] requestBody,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(requestBody);

        await using QuicStream requestStream = await connection
            .OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken)
            .ConfigureAwait(false);

        DebugLog($"managed H3 load request start path={endpoint.Path} bodyBytes={requestBody.Length}");
        Task<ManagedProofResponse> responseTask = ReadResponseAsync(requestStream, cancellationToken);
        await WriteRequestAsync(requestStream, endpoint, uri, requestBody, cancellationToken).ConfigureAwait(false);
        DebugLog("managed H3 load request written");
        return await responseTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        await qpackDecoderStream.DisposeAsync().ConfigureAwait(false);
        await qpackEncoderStream.DisposeAsync().ConfigureAwait(false);
        await controlStream.DisposeAsync().ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task<QuicClientConnectionOptions> CreateClientOptionsAsync(
        Uri uri,
        bool certificateBypassUsed)
    {
        QuicClientConnectionOptions options = new()
        {
            RemoteEndPoint = await ResolveRemoteEndPointAsync(uri).ConfigureAwait(false),
            MaxDatagramFrameSize = 64 * 1024,
            MaxInboundDatagramQueueSize = 0,
            MaxInboundUnidirectionalStreams = 3,
            InitialReceiveWindowSizes = new QuicReceiveWindowSizes
            {
                Connection = 16 * 1024 * 1024,
                LocallyInitiatedBidirectionalStream = 16 * 1024 * 1024,
                RemotelyInitiatedBidirectionalStream = 16 * 1024 * 1024,
                UnidirectionalStream = 16 * 1024 * 1024,
            },
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                AllowRenegotiation = false,
                AllowTlsResume = true,
                ApplicationProtocols = [SslApplicationProtocol.Http3],
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls13,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                TargetHost = uri.Host,
            },
        };

        if (certificateBypassUsed)
        {
            options.ClientAuthenticationOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
        }

        return options;
    }

    private static async Task<IPEndPoint> ResolveRemoteEndPointAsync(Uri uri)
    {
        if (IPAddress.TryParse(uri.Host, out IPAddress? parsedAddress))
        {
            return new IPEndPoint(parsedAddress, uri.Port);
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return new IPEndPoint(IPAddress.Loopback, uri.Port);
        }

        IPAddress[] addresses = await Dns.GetHostAddressesAsync(uri.Host).ConfigureAwait(false);
        IPAddress address = addresses.FirstOrDefault(static candidate => candidate.AddressFamily == AddressFamily.InterNetworkV6)
            ?? addresses.FirstOrDefault()
            ?? throw new InvalidOperationException($"Unable to resolve '{uri.Host}' for managed QUIC HTTP/3 proof.");

        return new IPEndPoint(address, uri.Port);
    }

    private static string BuildAuthority(Uri requestUri)
    {
        if (requestUri.IsDefaultPort)
        {
            return requestUri.IdnHost;
        }

        return string.Create(
            requestUri.IdnHost.Length + 1 + requestUri.Port.ToString(CultureInfo.InvariantCulture).Length,
            requestUri,
            static (destination, uri) =>
            {
                uri.IdnHost.AsSpan().CopyTo(destination);
                destination[uri.IdnHost.Length] = ':';
                uri.Port.TryFormat(destination[(uri.IdnHost.Length + 1)..], out _);
            });
    }

    private static string BuildPath(Uri requestUri)
    {
        string path = string.IsNullOrEmpty(requestUri.AbsolutePath) ? "/" : requestUri.AbsolutePath;
        return string.IsNullOrEmpty(requestUri.Query) ? path : path + requestUri.Query;
    }

    private static async ValueTask WriteStreamTypeAsync(
        QuicStream stream,
        Http3StreamType streamType,
        CancellationToken cancellationToken)
    {
        byte[] encoded = EncodeVariableLengthInteger(checked((ulong)streamType));
        await stream.WriteAsync(encoded, 0, encoded.Length, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] EncodeVariableLengthInteger(ulong value)
    {
        Span<byte> destination = stackalloc byte[Http3VariableLengthInteger.MaxEncodedLength];
        if (!Http3VariableLengthInteger.TryFormat(value, destination, out int bytesWritten))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return destination[..bytesWritten].ToArray();
    }

    private static IReadOnlyList<QPackFieldLine> BuildRequestHeaders(
        HttpEndpointSpec endpoint,
        Uri uri,
        byte[] requestBody)
    {
        List<QPackFieldLine> headers =
        [
            new(":method", endpoint.Method),
            new(":scheme", uri.Scheme),
            new(":authority", BuildAuthority(uri)),
            new(":path", BuildPath(uri)),
        ];

        bool contentLengthPresent = false;
        foreach (KeyValuePair<string, string> header in endpoint.RequestHeaders)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(header.Key, "content-length"))
            {
                contentLengthPresent = true;
            }

            // HTTP/3 requires header field names to be lowercase on the wire.
            headers.Add(new QPackFieldLine(header.Key.ToLowerInvariant(), header.Value));
        }

        if (requestBody.Length > 0 && !contentLengthPresent)
        {
            headers.Add(new QPackFieldLine("content-length", requestBody.Length.ToString(CultureInfo.InvariantCulture)));
        }

        return headers;
    }

    private static async Task WriteRequestAsync(
        QuicStream requestStream,
        HttpEndpointSpec endpoint,
        Uri uri,
        byte[] requestBody,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<QPackFieldLine> requestHeaders = BuildRequestHeaders(endpoint, uri, requestBody);
        byte[] requestHeaderSection = QPackEncoder.EncodeFieldSection(requestHeaders);
        byte[] headersFrame = Http3FrameWriter.WriteHeaders(requestHeaderSection);

        await QuicSendRetry.RetryTransientSendCreditAsync(
            writeToken => new ValueTask(requestStream.WriteAsync(headersFrame, 0, headersFrame.Length, writeToken)),
            "Timed out waiting for QUIC stream send credit.",
            "Timed out waiting for QUIC stream flow-control credit.",
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        if (requestBody.Length != 0)
        {
            const int requestDataFrameChunkSize = 1024;
            for (int offset = 0; offset < requestBody.Length; offset += requestDataFrameChunkSize)
            {
                int length = Math.Min(requestDataFrameChunkSize, requestBody.Length - offset);
                byte[] dataFrame = Http3FrameWriter.WriteData(requestBody.AsSpan(offset, length));
                await QuicSendRetry.RetryTransientSendCreditAsync(
                    writeToken => new ValueTask(requestStream.WriteAsync(dataFrame, 0, dataFrame.Length, writeToken)),
                    "Timed out waiting for QUIC stream send credit.",
                    "Timed out waiting for QUIC stream flow-control credit.",
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                await Task.Yield();
            }
        }

        await QuicSendRetry.RetryTransientSendCreditAsync(
            writeToken => requestStream.CompleteWritesAsync(writeToken),
            "Timed out waiting for QUIC stream FIN send credit.",
            "Timed out waiting for QUIC stream FIN flow-control credit.",
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        DebugLog("managed H3 load request stream completed");
    }

    private static async Task<ManagedProofResponse> ReadResponseAsync(
        QuicStream requestStream,
        CancellationToken cancellationToken)
    {
        Http3FrameReader frameReader = new();
        Http3ResponseSequenceValidator validator = new();
        byte[] buffer = new byte[16 * 1024];
        ArrayBufferWriter<byte> body = new();
        bool streamCompleted = false;

        while (true)
        {
            int bytesRead = await requestStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                DebugLog("managed H3 load response stream reached EOF");
                streamCompleted = true;
                foreach (Http3Frame frame in frameReader.Complete())
                {
                    ProcessResponseFrame(frame, validator, body);
                }

                break;
            }

            foreach (Http3Frame frame in frameReader.Read(buffer.AsSpan(0, bytesRead)))
            {
                ProcessResponseFrame(frame, validator, body);
            }

            if (TryCompleteOnContentLength(validator, body.WrittenCount))
            {
                DebugLog($"managed H3 load response complete via content-length bytes={body.WrittenCount}");
                break;
            }
        }

        IReadOnlyList<QPackFieldLine>? headers = validator.FinalResponseHeaders;
        if (headers is null)
        {
            throw new Http3Exception(Http3ErrorCode.MessageError, "The HTTP/3 response did not contain a HEADERS frame.");
        }

        if (streamCompleted)
        {
            validator.Complete();
        }
        else
        {
            Http3HeaderValidator.ValidateResponseHeaders(headers, checked((ulong)body.WrittenCount));
        }

        string? contentType = null;
        foreach (QPackFieldLine header in headers)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(header.Name, "content-type"))
            {
                contentType = header.Value;
                break;
            }
        }

        int statusCode = validator.FinalStatusCode!.Value;
        return new ManagedProofResponse(
            new Version(3, 0),
            (HttpStatusCode)statusCode,
            contentType,
            body.WrittenSpan.ToArray(),
            []);
    }

    private static void ProcessResponseFrame(
        Http3Frame frame,
        Http3ResponseSequenceValidator validator,
        IBufferWriter<byte> body)
    {
        switch (frame)
        {
            case Http3HeadersFrame headersFrame:
                validator.ReceiveHeaders(QPackDecoder.DecodeFieldSection(headersFrame.EncodedFieldSection));
                break;
            case Http3DataFrame dataFrame:
                validator.ReceiveData(checked((ulong)dataFrame.Data.Length));
                body.Write(dataFrame.Data.Span);
                break;
            case Http3UnknownFrame:
                break;
            default:
                throw new Http3Exception(Http3ErrorCode.FrameUnexpected, "The HTTP/3 response stream contained an invalid frame type.");
        }
    }

    private static void DebugLog(string message)
    {
        if (DebugLogging)
        {
            Console.Error.WriteLine(message);
        }
    }

    private static bool TryCompleteOnContentLength(
        Http3ResponseSequenceValidator validator,
        int receivedBodyLength)
    {
        IReadOnlyList<QPackFieldLine>? headers = validator.FinalResponseHeaders;
        if (headers is null)
        {
            return false;
        }

        if (!TryGetContentLength(headers, out ulong contentLength))
        {
            return false;
        }

        ulong receivedLength = checked((ulong)receivedBodyLength);
        if (receivedLength > contentLength)
        {
            throw new Http3Exception(Http3ErrorCode.MessageError, "The HTTP/3 response body exceeded Content-Length.");
        }

        return receivedLength == contentLength;
    }

    private static bool TryGetContentLength(IReadOnlyList<QPackFieldLine> headers, out ulong contentLength)
    {
        foreach (QPackFieldLine header in headers)
        {
            if (StringComparer.Ordinal.Equals(header.Name, "content-length"))
            {
                return ulong.TryParse(header.Value, out contentLength);
            }
        }

        contentLength = 0;
        return false;
    }
}
