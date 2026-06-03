// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Text.Json;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal static class ManagedHttp3LoadGenerator
{
    public const string ToolId = "managed-httpclient-h3-load";

    public static async Task<LoadToolRun> RunAsync(LoadToolExecutionPlan plan)
    {
        if (!string.Equals(plan.Semantics.Protocol, "h3", StringComparison.OrdinalIgnoreCase))
        {
            return new LoadToolRun(1, "", "managed-httpclient-h3-load supports H3 benchmark cells only.");
        }

        var warnings = new ConcurrentBag<string>();
        var errors = new ConcurrentBag<string>();
        var samples = new ConcurrentBag<double>();
        var statusCodes = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var certificateBypassUsed = ShouldBypassCertificateValidation(plan.TargetUrl, plan.CertificateMode);
        var requestBody = HttpScenarioValidator.CreateRequestBody(plan.Cell.Scenario.Endpoint!.RequestBodyGeneration);

        using var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(10),
            EnableMultipleHttp3Connections = true,
            SslOptions = new SslClientAuthenticationOptions()
        };

        if (certificateBypassUsed)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
        }

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (plan.EffectiveLoadShape.WarmupSeconds > 0)
        {
            await RunPhaseAsync(
                client,
                plan,
                requestBody,
                TimeSpan.FromSeconds(plan.EffectiveLoadShape.WarmupSeconds),
                record: false,
                samples,
                statusCodes,
                warnings,
                errors);
        }

        var measured = await RunPhaseAsync(
            client,
            plan,
            requestBody,
            TimeSpan.FromSeconds(plan.EffectiveLoadShape.DurationSeconds),
            record: true,
            samples,
            statusCodes,
            warnings,
            errors);

        var orderedSamples = samples.OrderBy(static value => value).ToArray();
        var metrics = new HttpMetrics
        {
            RequestsPerSecond = measured.Elapsed.TotalSeconds > 0 ? measured.TotalRequests / measured.Elapsed.TotalSeconds : null,
            TotalRequests = measured.TotalRequests,
            SuccessfulRequests = measured.SuccessfulRequests,
            FailedRequests = measured.FailedRequests,
            TimeoutRequests = measured.TimeoutRequests,
            StatusCodeCounts = new Dictionary<string, long>(statusCodes, StringComparer.OrdinalIgnoreCase),
            BytesReceived = measured.BytesReceived,
            ThroughputBytesPerSecond = measured.Elapsed.TotalSeconds > 0 ? measured.BytesReceived / measured.Elapsed.TotalSeconds : null,
            LatencyMinMs = orderedSamples.Length == 0 ? null : orderedSamples[0],
            LatencyMeanMs = orderedSamples.Length == 0 ? null : orderedSamples.Average(),
            LatencyP50Ms = Percentile(orderedSamples, 50),
            LatencyP75Ms = Percentile(orderedSamples, 75),
            LatencyP90Ms = Percentile(orderedSamples, 90),
            LatencyP95Ms = Percentile(orderedSamples, 95),
            LatencyP99Ms = Percentile(orderedSamples, 99),
            LatencyMaxMs = orderedSamples.Length == 0 ? null : orderedSamples[^1]
        };

        var responseVersionFailures = measured.ResponseVersionFailures;
        if (responseVersionFailures > 0)
        {
            warnings.Add($"managed-httpclient-h3-load observed {responseVersionFailures.ToString(CultureInfo.InvariantCulture)} non-H3 response version failures.");
        }

        if (certificateBypassUsed)
        {
            warnings.Add("Loopback certificate validation bypass was used for managed HTTP/3 load.");
        }

        var output = ResultJson.Serialize(new
        {
            tool = ToolId,
            category = LoadToolCategories.ManagedLab,
            requestedVersion = "3.0",
            versionPolicy = "RequestVersionExact",
            targetUrl = plan.TargetUrl.ToString(),
            certificateMode = certificateBypassUsed
                ? $"{plan.CertificateMode}; loopback-certificate-validation-bypass"
                : plan.CertificateMode,
            effectiveLoadShape = plan.EffectiveLoadShape,
            elapsedSeconds = measured.Elapsed.TotalSeconds,
            responseVersionFailures,
            metrics,
            warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            errors = errors.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        });

        return new LoadToolRun(0, output, string.Join(Environment.NewLine, errors));
    }

    private static async Task<ManagedRunCounters> RunPhaseAsync(
        HttpClient client,
        LoadToolExecutionPlan plan,
        byte[] requestBody,
        TimeSpan duration,
        bool record,
        ConcurrentBag<double> samples,
        ConcurrentDictionary<string, long> statusCodes,
        ConcurrentBag<string> warnings,
        ConcurrentBag<string> errors)
    {
        var concurrency = Math.Max(1, plan.EffectiveLoadShape.Concurrency);
        using var cts = new CancellationTokenSource(duration);
        var stopwatch = Stopwatch.StartNew();
        var counters = new ManagedRunCounters();
        var workers = Enumerable.Range(0, concurrency)
            .Select(_ => RunWorkerAsync(client, plan, requestBody, cts.Token, stopwatch, record, samples, statusCodes, counters, warnings, errors))
            .ToArray();

        await Task.WhenAll(workers);
        stopwatch.Stop();
        return counters with { Elapsed = stopwatch.Elapsed };
    }

    private static async Task RunWorkerAsync(
        HttpClient client,
        LoadToolExecutionPlan plan,
        byte[] requestBody,
        CancellationToken token,
        Stopwatch phaseStopwatch,
        bool record,
        ConcurrentBag<double> samples,
        ConcurrentDictionary<string, long> statusCodes,
        ManagedRunCounters counters,
        ConcurrentBag<string> warnings,
        ConcurrentBag<string> errors)
    {
        while (!token.IsCancellationRequested)
        {
            var requestStopwatch = Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(new HttpMethod(plan.Cell.Scenario.Endpoint!.Method), plan.TargetUrl)
                {
                    Version = HttpVersion.Version30,
                    VersionPolicy = HttpVersionPolicy.RequestVersionExact
                };

                if (requestBody.Length > 0)
                {
                    request.Content = new ByteArrayContent(requestBody);
                }

                foreach (var header in plan.Cell.Scenario.Endpoint!.RequestHeaders)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                var body = await HttpScenarioValidator.ReadResponseBodyAsync(response.Content, token);
                requestStopwatch.Stop();

                if (!record)
                {
                    continue;
                }

                Interlocked.Increment(ref counters.TotalRequests);
                Interlocked.Add(ref counters.BytesReceived, body.Length);
                statusCodes.AddOrUpdate(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture), 1, static (_, value) => value + 1);
                samples.Add(requestStopwatch.Elapsed.TotalMilliseconds);

                var responseVersion = FormatVersion(response.Version);
                var validationErrors = HttpScenarioValidator.ValidateResponse(
                    plan.Cell.Scenario.Endpoint,
                    response.StatusCode,
                    response.Content.Headers.ContentType?.MediaType,
                    body,
                    requestBody);

                if (!string.Equals(responseVersion, "3.0", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref counters.ResponseVersionFailures);
                    validationErrors.Add($"Expected HTTP/3 response version 3.0, got {responseVersion ?? "<missing>"}.");
                }

                if (validationErrors.Count == 0)
                {
                    Interlocked.Increment(ref counters.SuccessfulRequests);
                }
                else
                {
                    Interlocked.Increment(ref counters.FailedRequests);
                    foreach (var error in validationErrors.Take(5))
                    {
                        warnings.Add(error);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (TaskCanceledException ex)
            {
                if (record)
                {
                    Interlocked.Increment(ref counters.TotalRequests);
                    Interlocked.Increment(ref counters.FailedRequests);
                    Interlocked.Increment(ref counters.TimeoutRequests);
                    errors.Add($"Managed H3 request timed out: {ex.Message}");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or NotSupportedException or IOException)
            {
                if (record)
                {
                    Interlocked.Increment(ref counters.TotalRequests);
                    Interlocked.Increment(ref counters.FailedRequests);
                    errors.Add($"Managed H3 request failed: {ex.Message}");
                }
            }
        }
    }

    private static bool ShouldBypassCertificateValidation(Uri uri, string certificateMode)
    {
        if (!IsLoopback(uri))
        {
            return false;
        }

        return certificateMode.Contains("development", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("local", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("loopback", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("self-signed", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("bypass", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoopback(Uri uri)
    {
        return IPAddress.TryParse(uri.Host, out var address)
            ? IPAddress.IsLoopback(address)
            : string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FormatVersion(Version? version)
    {
        return version is null
            ? null
            : $"{version.Major.ToString(CultureInfo.InvariantCulture)}.{version.Minor.ToString(CultureInfo.InvariantCulture)}";
    }

    private static double? Percentile(double[] orderedSamples, double percentile)
    {
        if (orderedSamples.Length == 0)
        {
            return null;
        }

        if (orderedSamples.Length == 1)
        {
            return orderedSamples[0];
        }

        var rank = (percentile / 100d) * (orderedSamples.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper)
        {
            return orderedSamples[lower];
        }

        return orderedSamples[lower] + ((orderedSamples[upper] - orderedSamples[lower]) * (rank - lower));
    }

    private sealed record ManagedRunCounters
    {
        public long TotalRequests;
        public long SuccessfulRequests;
        public long FailedRequests;
        public long TimeoutRequests;
        public long BytesReceived;
        public long ResponseVersionFailures;
        public TimeSpan Elapsed { get; init; }
    }
}

internal static class ManagedHttp3JsonParser
{
    public static LoadToolParseResult Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new LoadToolParseResult(false, new HttpMetrics(), ["managed-httpclient-h3-load output was empty."]);
        }

        try
        {
            using var document = JsonDocument.Parse(output);
            var metricsElement = FindObject(document.RootElement, "metrics");
            if (metricsElement is null)
            {
                return new LoadToolParseResult(false, new HttpMetrics(), ["managed-httpclient-h3-load output did not contain metrics."]);
            }

            var metrics = new HttpMetrics
            {
                RequestsPerSecond = FindNumber(metricsElement.Value, "requestsPerSecond"),
                TotalRequests = FindLong(metricsElement.Value, "totalRequests"),
                SuccessfulRequests = FindLong(metricsElement.Value, "successfulRequests"),
                FailedRequests = FindLong(metricsElement.Value, "failedRequests"),
                TimeoutRequests = FindLong(metricsElement.Value, "timeoutRequests"),
                BytesReceived = FindLong(metricsElement.Value, "bytesReceived"),
                ThroughputBytesPerSecond = FindNumber(metricsElement.Value, "throughputBytesPerSecond"),
                LatencyMinMs = FindNumber(metricsElement.Value, "latencyMinMs"),
                LatencyMeanMs = FindNumber(metricsElement.Value, "latencyMeanMs"),
                LatencyP50Ms = FindNumber(metricsElement.Value, "latencyP50Ms"),
                LatencyP75Ms = FindNumber(metricsElement.Value, "latencyP75Ms"),
                LatencyP90Ms = FindNumber(metricsElement.Value, "latencyP90Ms"),
                LatencyP95Ms = FindNumber(metricsElement.Value, "latencyP95Ms"),
                LatencyP99Ms = FindNumber(metricsElement.Value, "latencyP99Ms"),
                LatencyMaxMs = FindNumber(metricsElement.Value, "latencyMaxMs"),
                StatusCodeCounts = FindStatusCodeCounts(metricsElement.Value)
            };

            var warnings = ReadStringArray(document.RootElement, "warnings").ToList();
            warnings.AddRange(ReadStringArray(document.RootElement, "errors"));
            var responseVersionFailures = FindLong(document.RootElement, "responseVersionFailures");
            if (responseVersionFailures > 0)
            {
                warnings.Add($"managed-httpclient-h3-load recorded {responseVersionFailures.Value.ToString(CultureInfo.InvariantCulture)} response version failures.");
            }

            var parsed = metrics.RequestsPerSecond is not null || metrics.TotalRequests is not null;
            return new LoadToolParseResult(
                parsed,
                metrics,
                parsed ? warnings : [.. warnings, "managed-httpclient-h3-load output was preserved, but no metrics were parsed."]);
        }
        catch (JsonException ex)
        {
            return new LoadToolParseResult(false, new HttpMetrics(), [$"managed-httpclient-h3-load output was not valid JSON: {ex.Message}"]);
        }
    }

    private static JsonElement? FindObject(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Object)
            {
                return property.Value;
            }
        }

        return null;
    }

    private static long? FindLong(JsonElement element, string name)
    {
        var value = FindNumber(element, name);
        return value.HasValue ? Convert.ToInt64(value.Value, CultureInfo.InvariantCulture) : null;
    }

    private static double? FindNumber(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var number))
            {
                return number;
            }
        }

        return null;
    }

    private static Dictionary<string, long> FindStatusCodeCounts(JsonElement metricsElement)
    {
        var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (!metricsElement.TryGetProperty("statusCodeCounts", out var statusCodes) ||
            statusCodes.ValueKind != JsonValueKind.Object)
        {
            return counts;
        }

        foreach (var property in statusCodes.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Number &&
                property.Value.TryGetInt64(out var count))
            {
                counts[property.Name] = count;
            }
        }

        return counts;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array
            .EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString()!)
            .ToArray();
    }
}
