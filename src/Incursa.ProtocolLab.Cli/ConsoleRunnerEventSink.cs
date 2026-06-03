// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Runner;

namespace Incursa.ProtocolLab.Cli;

internal sealed class ConsoleRunnerEventSink : IRunnerEventSink
{
    private static readonly object ConsoleLock = new();

    public void OnEvent(RunnerEvent runnerEvent)
    {
        lock (ConsoleLock)
        {
            if (runnerEvent.Severity == RunnerMessageSeverity.Error)
            {
                Console.Error.WriteLine(runnerEvent.Message);
                Console.Error.Flush();
                return;
            }

            Console.Out.WriteLine(runnerEvent.Message);
            Console.Out.Flush();
        }
    }
}
