// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public static class ProtocolIds
{
    public const string Http1 = "h1";
    public const string Http2 = "h2";
    public const string Http3 = "h3";
    public const string Quic = "quic";
    public const string WebTransport = "webtransport";
    public const string Masque = "masque";

    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["h1"] = Http1,
        ["http1"] = Http1,
        ["http/1.1"] = Http1,
        ["h2"] = Http2,
        ["http2"] = Http2,
        ["http/2"] = Http2,
        ["h3"] = Http3,
        ["http3"] = Http3,
        ["http/3"] = Http3,
        ["quic"] = Quic,
        ["webtransport"] = WebTransport,
        ["wt"] = WebTransport,
        ["masque"] = Masque
    };

    public static string Normalize(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return string.Empty;
        }

        var trimmed = protocol.Trim();
        return Aliases.TryGetValue(trimmed, out var canonical)
            ? canonical
            : trimmed.ToLowerInvariant();
    }

    public static bool IsHttp1(string? protocol) => Is(protocol, Http1);
    public static bool IsHttp2(string? protocol) => Is(protocol, Http2);
    public static bool IsHttp3(string? protocol) => Is(protocol, Http3);
    public static bool IsQuic(string? protocol) => Is(protocol, Quic);

    public static bool Is(string? protocol, string canonicalId)
    {
        return string.Equals(Normalize(protocol), canonicalId, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsKnown(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return false;
        }

        var canonical = Normalize(protocol);
        return string.Equals(canonical, Http1, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonical, Http2, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonical, Http3, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonical, Quic, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonical, WebTransport, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonical, Masque, StringComparison.OrdinalIgnoreCase);
    }
}
