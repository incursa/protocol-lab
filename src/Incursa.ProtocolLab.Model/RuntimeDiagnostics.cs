// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Model;

public static class DiagnosticTargetResolutionStrategies
{
    public const string RootProcess = "root-process";
    public const string ChildDotnetProcess = "child-dotnet-process";
    public const string CommandLineMatch = "command-line-match";
    public const string DotnetCountersPsMatch = "dotnet-counters-ps-match";
    public const string ManifestPidFile = "manifest-pid-file";
    public const string StdoutRegex = "stdout-regex";
    public const string ExplicitPid = "explicit-pid";
    public const string Unresolved = "unresolved";
}

public static class DiagnosticTargetConfidenceLevels
{
    public const string High = "high";
    public const string Medium = "medium";
    public const string Low = "low";
}

public static class CounterCaptureStatuses
{
    public const string Disabled = "disabled";
    public const string Succeeded = "succeeded";
    public const string ToolUnavailable = "tool-unavailable";
    public const string TargetUnresolved = "target-unresolved";
    public const string Failed = "failed";
    public const string NotRun = "not-run";
}

public sealed record DiagnosticTarget
{
    public int? RootProcessId { get; init; }
    public int? ResolvedProcessId { get; init; }
    public string? ResolvedProcessName { get; init; }
    public string ResolutionStrategy { get; init; } = DiagnosticTargetResolutionStrategies.Unresolved;
    public string? CommandLine { get; init; }
    public string? ExecutablePath { get; init; }
    public string? WorkingDirectory { get; init; }
    public string Confidence { get; init; } = DiagnosticTargetConfidenceLevels.Low;
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed record RuntimeCounterSummary
{
    public int Samples { get; init; }
    public DateTimeOffset? CollectionStartUtc { get; init; }
    public DateTimeOffset? CollectionEndUtc { get; init; }
    public double? CpuMean { get; init; }
    public double? CpuMax { get; init; }
    public double? AllocatedBytesDelta { get; init; }
    public double? AllocationRateMean { get; init; }
    public double? Gen0CollectionsDelta { get; init; }
    public double? Gen1CollectionsDelta { get; init; }
    public double? Gen2CollectionsDelta { get; init; }
    public double? GcHeapSizeMean { get; init; }
    public double? GcHeapSizeMax { get; init; }
    public double? GcPauseTimeDelta { get; init; }
    public double? ThreadPoolThreadCountMean { get; init; }
    public double? ThreadPoolQueueLengthMax { get; init; }
    public double? ExceptionCountDelta { get; init; }
    public double? ExceptionRateMean { get; init; }
    public IReadOnlyList<string> ParseWarnings { get; init; } = [];
}
