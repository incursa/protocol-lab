// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public sealed class SuiteDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Purpose { get; init; } = "";
    public string ResultKind { get; init; } = "";
    public string TargetMode { get; init; } = "";
    public string TargetNetworkMode { get; init; } = "";
    public List<string> Implementations { get; init; } = [];
    public List<SuiteUnsupportedImplementation> UnsupportedImplementations { get; init; } = [];
    public List<string> Scenarios { get; init; } = [];
    public string Protocol { get; init; } = "";
    public string? LoadProfileId { get; init; }
    public List<SuiteLoadToolDefinition> LoadTools { get; init; } = [];
    public SuiteDefaults Defaults { get; init; } = new();
    public SuiteCounterCapture CounterCapture { get; init; } = new();
    public string Notes { get; init; } = "";
}

public sealed class SuiteLoadToolDefinition
{
    public string Id { get; init; } = "";
    public string Mode { get; init; } = "";
    public string Category { get; init; } = "";
}

public sealed class SuiteUnsupportedImplementation
{
    public string Id { get; init; } = "";
    public string Reason { get; init; } = "";
}

public sealed class SuiteDefaults
{
    public int DurationSeconds { get; init; }
    public int WarmupSeconds { get; init; }
    public int Repetitions { get; init; }
    public int Connections { get; init; }
    public int StreamsPerConnection { get; init; }
}

public sealed class SuiteCounterCapture
{
    public bool EnabledByDefault { get; init; }
    public string Tool { get; init; } = "";
}
