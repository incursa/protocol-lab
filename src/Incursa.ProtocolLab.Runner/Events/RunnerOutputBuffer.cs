// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Runner;

internal sealed class RunnerOutputBuffer
{
    private readonly RunnerCommandKind commandKind;
    private readonly IRunnerEventSink eventSink;
    private readonly List<RunnerMessage> messages = [];

    public RunnerOutputBuffer(RunnerCommandKind commandKind, IRunnerEventSink? eventSink)
    {
        this.commandKind = commandKind;
        this.eventSink = eventSink ?? NoopRunnerEventSink.Instance;
    }

    public IReadOnlyList<RunnerMessage> Messages => messages;

    public void WriteLine(string text = "")
    {
        Add(RunnerMessageSeverity.Information, text);
    }

    public void WriteWarning(string text)
    {
        Add(RunnerMessageSeverity.Warning, text);
    }

    public void WriteError(string text)
    {
        Add(RunnerMessageSeverity.Error, text);
    }

    public void Append(RunnerMessage message)
    {
        messages.Add(message);
    }

    private void Add(RunnerMessageSeverity severity, string text)
    {
        var message = new RunnerMessage(severity, text);
        messages.Add(message);
        if (text.Length > 0)
        {
            eventSink.OnEvent(new RunnerEvent(commandKind, severity, text, DateTimeOffset.UtcNow));
        }
    }
}
