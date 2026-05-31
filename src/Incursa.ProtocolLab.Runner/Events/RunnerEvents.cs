// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Runner;

public sealed record RunnerEvent(
    RunnerCommandKind CommandKind,
    RunnerMessageSeverity Severity,
    string Message,
    DateTimeOffset Timestamp);

public interface IRunnerEventSink
{
    void OnEvent(RunnerEvent runnerEvent);
}

public sealed class NoopRunnerEventSink : IRunnerEventSink
{
    public static NoopRunnerEventSink Instance { get; } = new();

    private NoopRunnerEventSink()
    {
    }

    public void OnEvent(RunnerEvent runnerEvent)
    {
    }
}

public sealed class RecordingRunnerEventSink : IRunnerEventSink
{
    private readonly List<RunnerEvent> events = [];

    public IReadOnlyList<RunnerEvent> Events => events;

    public void OnEvent(RunnerEvent runnerEvent)
    {
        events.Add(runnerEvent);
    }
}
