// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Incursa.ProtocolLab.Tests.Fixtures.RunnerContractLab;

internal sealed class FixtureHttpEndpointHost : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task acceptLoop;
    private readonly string body;

    private FixtureHttpEndpointHost(int port, string body)
    {
        this.body = body;
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        BaseUrl = $"http://127.0.0.1:{endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        acceptLoop = AcceptLoopAsync(cancellation.Token);
    }

    public string BaseUrl { get; }

    public static async Task<FixtureHttpEndpointHost> StartAsync(int port, string body = "fixture-ok")
    {
        var host = new FixtureHttpEndpointHost(port, body);
        await host.WaitForReadyAsync();
        return host;
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        listener.Stop();

        try
        {
            await acceptLoop;
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        cancellation.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                client?.Dispose();
                break;
            }
            catch (ObjectDisposedException)
            {
                client?.Dispose();
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var ownedClient = client;
        try
        {
            var stream = ownedClient.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null || line.Length == 0)
                {
                    break;
                }
            }

            var payload = Encoding.UTF8.GetBytes(body);
            var header = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)}\r\nConnection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
    }

    private async Task WaitForReadyAsync()
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(250)
        };

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync($"{BaseUrl}/ok");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException)
            {
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Fixture HTTP endpoint did not start on {BaseUrl}.");
    }
}
