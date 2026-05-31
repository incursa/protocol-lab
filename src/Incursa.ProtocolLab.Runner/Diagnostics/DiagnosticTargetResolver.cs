// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal static class DiagnosticTargetResolver
{
    public static DiagnosticTarget Resolve(Process? rootProcess, TargetExecutionResult targetResult)
    {
        if (rootProcess is null || targetResult.ProcessId is null)
        {
            return new DiagnosticTarget
            {
                RootProcessId = targetResult.ProcessId,
                ResolutionStrategy = DiagnosticTargetResolutionStrategies.Unresolved,
                Confidence = DiagnosticTargetConfidenceLevels.Low,
                CommandLine = targetResult.CommandLine,
                ExecutablePath = targetResult.ExecutablePath,
                WorkingDirectory = targetResult.WorkingDirectory,
                Warnings = ["diagnostic-target-unresolved: target is external, containerized, or no host process was started."]
            };
        }

        var commandLine = targetResult.CommandLine ?? "";
        var isDotnetRunWrapper = commandLine.Contains("dotnet", StringComparison.OrdinalIgnoreCase) &&
            commandLine.Contains(" run ", StringComparison.OrdinalIgnoreCase);
        if (isDotnetRunWrapper)
        {
            return new DiagnosticTarget
            {
                RootProcessId = targetResult.ProcessId,
                ResolutionStrategy = DiagnosticTargetResolutionStrategies.Unresolved,
                Confidence = DiagnosticTargetConfidenceLevels.Low,
                CommandLine = commandLine,
                ExecutablePath = targetResult.ExecutablePath,
                WorkingDirectory = targetResult.WorkingDirectory,
                Warnings =
                [
                    "diagnostic-target-unresolved: root process appears to be a dotnet run wrapper; counters are not attached to wrapper processes."
                ]
            };
        }

        string? processName = null;
        try
        {
            processName = rootProcess.ProcessName;
        }
        catch (InvalidOperationException)
        {
        }

        return new DiagnosticTarget
        {
            RootProcessId = targetResult.ProcessId,
            ResolvedProcessId = targetResult.ProcessId,
            ResolvedProcessName = processName,
            ResolutionStrategy = DiagnosticTargetResolutionStrategies.RootProcess,
            CommandLine = commandLine,
            ExecutablePath = targetResult.ExecutablePath,
            WorkingDirectory = targetResult.WorkingDirectory,
            Confidence = DiagnosticTargetConfidenceLevels.High
        };
    }
}
