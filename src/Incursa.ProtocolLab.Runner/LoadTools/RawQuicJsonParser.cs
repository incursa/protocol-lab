// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.Json;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal static class RawQuicJsonParser
{
    public static LoadToolParseResult Parse(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return new LoadToolParseResult(false, new HttpMetrics(), ["raw-quic-json output was empty."]);
        }

        try
        {
            var jsonText = ExtractFirstJsonObject(stdout.TrimStart());
            using var document = JsonDocument.Parse(jsonText);
            var metricsElement = FindObject(document.RootElement, "metrics") ?? document.RootElement;
            var metrics = new HttpMetrics
            {
                RequestsPerSecond = FindNumber(metricsElement, "requestsPerSecond", "requestsPerSec", "req/s", "rps"),
                TotalRequests = FindLong(metricsElement, "totalRequests", "total", "requests"),
                SuccessfulRequests = FindLong(metricsElement, "successfulRequests", "succeeded", "success", "done"),
                FailedRequests = FindLong(metricsElement, "failedRequests", "failed"),
                TimeoutRequests = FindLong(metricsElement, "timeoutRequests", "timeout", "timeouts"),
                BytesReceived = FindLong(metricsElement, "bytesReceived", "receivedBytes", "totalBytesReceived"),
                BytesSent = FindLong(metricsElement, "bytesSent", "sentBytes", "totalBytesSent"),
                ThroughputBytesPerSecond = FindNumber(metricsElement, "throughputBytesPerSecond", "bytesPerSecond", "throughput"),
                LatencyMinMs = FindDurationMilliseconds(metricsElement, "latencyMinMs", "min"),
                LatencyMeanMs = FindDurationMilliseconds(metricsElement, "latencyMeanMs", "mean", "average"),
                LatencyP50Ms = FindDurationMilliseconds(metricsElement, "latencyP50Ms", "p50", "median", "50"),
                LatencyP75Ms = FindDurationMilliseconds(metricsElement, "latencyP75Ms", "p75", "75"),
                LatencyP90Ms = FindDurationMilliseconds(metricsElement, "latencyP90Ms", "p90", "90"),
                LatencyP95Ms = FindDurationMilliseconds(metricsElement, "latencyP95Ms", "p95", "95"),
                LatencyP99Ms = FindDurationMilliseconds(metricsElement, "latencyP99Ms", "p99", "99"),
                LatencyMaxMs = FindDurationMilliseconds(metricsElement, "latencyMaxMs", "max"),
                ConnectTimeMeanMs = FindDurationMilliseconds(metricsElement, "connectTimeMeanMs", "connectMeanMs", "connectMean"),
                TimeToFirstByteMeanMs = FindDurationMilliseconds(metricsElement, "timeToFirstByteMeanMs", "ttfbMeanMs", "timeToFirstByteMean"),
                StatusCodeCounts = FindStatusCodeCounts(metricsElement)
            };

            var warnings = ReadStringArray(document.RootElement, "warnings").ToList();
            warnings.AddRange(ReadStringArray(document.RootElement, "notes"));
            warnings.AddRange(ReadStringArray(document.RootElement, "errors"));
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                warnings.AddRange(
                    stderr
                        .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(line => !string.IsNullOrWhiteSpace(line)));
            }

            var parsed = metrics.RequestsPerSecond is not null ||
                metrics.TotalRequests is not null ||
                metrics.SuccessfulRequests is not null ||
                metrics.BytesReceived is not null ||
                metrics.BytesSent is not null ||
                metrics.LatencyMeanMs is not null ||
                metrics.ThroughputBytesPerSecond is not null;

            if (!parsed)
            {
                warnings.Add("raw-quic-json output was preserved, but no metrics were parsed.");
            }

            return new LoadToolParseResult(parsed, metrics, warnings);
        }
        catch (JsonException ex)
        {
            return new LoadToolParseResult(false, new HttpMetrics(), [$"raw-quic-json output was not valid JSON: {ex.Message}"]);
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

    private static IEnumerable<string> ReadStringArray(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }

    private static long? FindLong(JsonElement element, params string[] names)
    {
        var value = FindNumber(element, names);
        return value.HasValue ? Convert.ToInt64(value.Value, CultureInfo.InvariantCulture) : null;
    }

    private static double? FindDurationMilliseconds(JsonElement element, params string[] names)
    {
        var value = FindNumber(element, names);
        return value;
    }

    private static double? FindNumber(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) &&
                    TryGetNumber(property.Value, out var direct))
                {
                    return direct;
                }

                var nested = FindNumber(property.Value, names);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindNumber(item, names);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static bool TryGetNumber(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDouble(out value);
        }

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static Dictionary<string, long> FindStatusCodeCounts(JsonElement element)
    {
        var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        FindStatusCodeCounts(element, counts);
        return counts;
    }

    private static void FindStatusCodeCounts(JsonElement element, Dictionary<string, long> counts)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if ((property.Name.Contains("status", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Contains("code", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var status in property.Value.EnumerateObject())
                    {
                        if (status.Value.ValueKind == JsonValueKind.Number &&
                            status.Value.TryGetInt64(out var count) &&
                            (int.TryParse(status.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                             status.Name.EndsWith("xx", StringComparison.OrdinalIgnoreCase)))
                        {
                            counts[status.Name] = count;
                        }
                    }
                }

                FindStatusCodeCounts(property.Value, counts);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                FindStatusCodeCounts(item, counts);
            }
        }
    }

    private static string ExtractFirstJsonObject(string output)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var index = 0; index < output.Length; index++)
        {
            var current = output[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return output[..(index + 1)];
                }
            }
        }

        return output;
    }
}
