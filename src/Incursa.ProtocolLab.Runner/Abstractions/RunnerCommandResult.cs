// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Runner;

public enum RunnerCommandKind
{
    Check,
    List,
    Validate,
    Run,
    Report
}

public enum RunnerMessageSeverity
{
    Information,
    Warning,
    Error
}

public sealed record RunnerMessage(
    RunnerMessageSeverity Severity,
    string Text);

public sealed record RunnerArtifactReference(
    string Kind,
    string Path);

public sealed record RunnerCommandResult(
    RunnerCommandKind Kind,
    int ExitCode,
    IReadOnlyList<RunnerMessage> Messages,
    IReadOnlyList<RunnerArtifactReference> Artifacts)
{
    public static RunnerCommandResult Create(
        RunnerCommandKind kind,
        int exitCode,
        IReadOnlyList<RunnerMessage> messages,
        IReadOnlyList<RunnerArtifactReference>? artifacts = null)
    {
        return new RunnerCommandResult(kind, exitCode, messages, artifacts ?? []);
    }
}
