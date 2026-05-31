// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Runner;

namespace Incursa.ProtocolLab.Cli;

internal static class RunnerConsoleRenderer
{
    public static void Render(RunnerCommandResult result)
    {
        foreach (var message in result.Messages)
        {
            if (message.Severity == RunnerMessageSeverity.Error)
            {
                Console.Error.WriteLine(message.Text);
            }
            else
            {
                Console.WriteLine(message.Text);
            }
        }
    }
}
