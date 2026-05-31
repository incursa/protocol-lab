// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Runner;

namespace Incursa.ProtocolLab.Cli;

internal sealed class CliOptions
{
    private readonly Dictionary<string, string> values;

    private CliOptions(Dictionary<string, string> values)
    {
        this.values = values;
    }

    public string? Get(string name)
    {
        return values.TryGetValue(name, out var value) ? value : null;
    }

    public RunnerCommandOptions ToRunnerOptions()
    {
        return new RunnerCommandOptions(values);
    }

    public static CliOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var name = current[2..];
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values[name] = "true";
                continue;
            }

            values[name] = args[++index];
        }

        return new CliOptions(values);
    }
}
