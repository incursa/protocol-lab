// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Incursa.ProtocolLab.Runner;

public sealed class RunnerCommandOptions
{
    private readonly Dictionary<string, string> values;

    public RunnerCommandOptions(IReadOnlyDictionary<string, string> values)
    {
        this.values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    public string? Get(string name)
    {
        return values.TryGetValue(name, out var value) ? value : null;
    }
}
