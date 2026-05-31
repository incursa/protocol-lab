// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed record RequestedLoadShape
{
    public required int Connections { get; init; }
    public required int Concurrency { get; init; }
    public required int StreamsPerConnection { get; init; }
    public long? Requests { get; init; }
    public required int DurationSeconds { get; init; }
    public required int WarmupSeconds { get; init; }
    public required int Repetitions { get; init; }
}

public sealed record EffectiveLoadShape
{
    public required int Connections { get; init; }
    public required int Concurrency { get; init; }
    public required int StreamsPerConnection { get; init; }
    public long? Requests { get; init; }
    public required int DurationSeconds { get; init; }
    public required int WarmupSeconds { get; init; }
    public required int Repetitions { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record LoadShapeSemantics
{
    public required string Protocol { get; init; }
    public string? LoadTool { get; init; }
    public IReadOnlyList<string> SupportedFields { get; init; } = [];
    public IReadOnlyList<string> IgnoredFields { get; init; } = [];
    public IReadOnlyList<string> DerivedFields { get; init; } = [];
    public IReadOnlyList<string> UnsupportedFields { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
