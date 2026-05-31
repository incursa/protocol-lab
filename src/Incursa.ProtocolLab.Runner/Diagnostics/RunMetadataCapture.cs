// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal static class RunMetadataCapture
{
    public static async Task<RunMetadata> CaptureAsync()
    {
        var warnings = new List<string>();
        var dockerVersion = await CaptureDockerVersionAsync(warnings);
        var dockerBackend = await CaptureDockerBackendAsync(warnings);
        var memoryInfo = GC.GetGCMemoryInfo();
        long? totalAvailableMemoryBytes = memoryInfo.TotalAvailableMemoryBytes > 0
            ? memoryInfo.TotalAvailableMemoryBytes
            : null;

        using var process = Process.GetCurrentProcess();
        process.Refresh();

        return new RunMetadata(
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.OSArchitecture.ToString(),
            Environment.ProcessorCount,
            Environment.Is64BitProcess,
            process.WorkingSet64,
            totalAvailableMemoryBytes,
            dockerVersion,
            Environment.GetEnvironmentVariable("PROTOCOL_LAB_NETWORK_MODE"),
            dockerBackend,
            process.Id,
            DateTimeOffset.UtcNow,
            await CaptureGitAsync(["rev-parse", "--short", "HEAD"], warnings),
            await CaptureGitAsync(["status", "--short"], warnings),
            warnings);
    }

    private static async Task<string?> CaptureDockerVersionAsync(ICollection<string> warnings)
    {
        var docker = LoadToolInvoker.ResolveExecutable("docker");
        if (docker is null)
        {
            warnings.Add("Docker executable was not found on PATH; docker metadata was not captured.");
            return null;
        }

        var output = await RunCommandAsync(docker, ["--version"], TimeSpan.FromSeconds(3));
        if (output is null)
        {
            warnings.Add("Docker version metadata could not be captured.");
        }

        return output;
    }

    private static async Task<string?> CaptureDockerBackendAsync(ICollection<string> warnings)
    {
        var docker = LoadToolInvoker.ResolveExecutable("docker");
        if (docker is null)
        {
            return null;
        }

        var output = await RunCommandAsync(docker, ["info", "--format", "{{.Driver}}|{{.OperatingSystem}}"], TimeSpan.FromSeconds(3));
        if (output is null)
        {
            warnings.Add("Docker backend metadata could not be captured.");
        }

        return output;
    }

    private static async Task<string?> CaptureGitAsync(IReadOnlyList<string> arguments, ICollection<string> warnings)
    {
        var git = LoadToolInvoker.ResolveExecutable("git");
        if (git is null)
        {
            warnings.Add("Git executable was not found on PATH; git metadata was not captured.");
            return null;
        }

        var output = await RunCommandAsync(git, arguments, TimeSpan.FromSeconds(3));
        if (output is null)
        {
            warnings.Add($"Git metadata command '{string.Join(" ", arguments)}' could not be captured.");
        }

        return output;
    }

    private static async Task<string?> RunCommandAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var exitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(exitTask, Task.Delay(timeout));

        if (completed != exitTask)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            catch
            {
                // Best effort only.
            }

            return null;
        }

        await exitTask;
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var output = !string.IsNullOrWhiteSpace(stdout) ? stdout : stderr;
        return string.IsNullOrWhiteSpace(output) ? null : output.Trim();
    }
}
