// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal sealed class TargetHandle : IAsyncDisposable
{
    private readonly Process? process;
    private readonly Task stdoutTask;
    private readonly Task stderrTask;
    private readonly string? dockerExecutable;
    private readonly string? containerName;
    private readonly ArtifactPaths? paths;
    private TargetExecutionResult result;

    public TargetHandle(string baseUrl, TargetExecutionResult result)
    {
        BaseUrl = baseUrl;
        this.result = result;
        stdoutTask = Task.CompletedTask;
        stderrTask = Task.CompletedTask;
    }

    public TargetHandle(
        string baseUrl,
        Process process,
        Task stdoutTask,
        Task stderrTask,
        TargetExecutionResult result)
    {
        BaseUrl = baseUrl;
        this.process = process;
        this.stdoutTask = stdoutTask;
        this.stderrTask = stderrTask;
        this.result = result;
    }

    public TargetHandle(
        string baseUrl,
        string dockerExecutable,
        string containerName,
        ArtifactPaths paths,
        TargetExecutionResult result)
    {
        BaseUrl = baseUrl;
        this.dockerExecutable = dockerExecutable;
        this.containerName = containerName;
        this.paths = paths;
        this.result = result;
        stdoutTask = Task.CompletedTask;
        stderrTask = Task.CompletedTask;
    }

    public string BaseUrl { get; }

    public Process? Process => process;

    public TargetExecutionResult Result => result;

    public async ValueTask DisposeAsync()
    {
        if (process is null)
        {
            if (!string.IsNullOrWhiteSpace(dockerExecutable) &&
                !string.IsNullOrWhiteSpace(containerName) &&
                paths is not null)
            {
                result = await TargetOrchestrator.StopDockerTargetAsync(
                    dockerExecutable,
                    containerName,
                    paths,
                    result);
            }

            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
        }

        await Task.WhenAll(stdoutTask, stderrTask);

        result = result with
        {
            ExitCode = process.HasExited ? process.ExitCode : result.ExitCode
        };

        process.Dispose();
    }
}

internal static class TargetOrchestrator
{
    public static async Task<TargetHandle> StartAsync(
        string root,
        ImplementationManifest manifest,
        string? externalBaseUrl,
        ArtifactPaths paths,
        string? requestedProtocol = null,
        TargetStartOptions? options = null)
    {
        options ??= TargetStartOptions.Default;
        Directory.CreateDirectory(paths.CellDirectory);
        EnsureTextFile(paths.TargetStdout);
        EnsureTextFile(paths.TargetStderr);
        EnsureTextFile(paths.ServerStdout);
        EnsureTextFile(paths.ServerStderr);
        EnsureTextFile(paths.TargetDockerCommandTxt);
        EnsureTextFile(paths.TargetDockerInspectJson);
        EnsureTextFile(paths.TargetDockerNetworkInspectJson);
        EnsureTextFile(paths.DockerNetworkCommandTxt);
        EnsureTextFile(paths.DockerNetworkInspectJson);
        EnsureTextFile(paths.DockerNetworkCleanupTxt);

        var requestedMode = ResolveRequestedMode(manifest, externalBaseUrl, options);

        if (string.Equals(requestedMode, TargetKinds.External, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(externalBaseUrl))
            {
                var unsupported = TargetExecutionResult.UnsupportedResult("External target mode requires --base-url.")
                    with
                    {
                        TargetExecutionMode = TargetKinds.External,
                        StdoutPath = paths.TargetStdout,
                        StderrPath = paths.TargetStderr,
                        LogsPath = paths.CellDirectory
                    };
                await WriteTargetExecutionAsync(paths, unsupported);
                return new TargetHandle("", unsupported);
            }

            var externalResult = TargetExecutionResult.ExternalReady(externalBaseUrl)
                with
                {
                    StdoutPath = paths.TargetStdout,
                    StderrPath = paths.TargetStderr,
                    LogsPath = paths.CellDirectory
                };
            await WriteTargetExecutionAsync(paths, externalResult);
            return new TargetHandle(externalBaseUrl, externalResult);
        }

        if (string.Equals(requestedMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            return await StartDockerAsync(root, manifest, paths, ResolveBaseUrl(manifest, requestedProtocol, TargetKinds.Docker), requestedProtocol, options);
        }

        if (!string.Equals(requestedMode, TargetKinds.Process, StringComparison.OrdinalIgnoreCase))
        {
            var resolvedBaseUrl = ResolveBaseUrl(manifest, requestedProtocol);
            var reason = string.IsNullOrWhiteSpace(manifest.TargetKind)
                ? $"Implementation '{manifest.Id}' does not define target startup."
                : $"Target kind '{requestedMode}' is not supported by target orchestration.";
            var unsupportedResult = TargetExecutionResult.UnsupportedResult(reason)
                with
                {
                    TargetExecutionMode = requestedMode,
                    StdoutPath = paths.TargetStdout,
                    StderrPath = paths.TargetStderr,
                    LogsPath = paths.CellDirectory
            };
            await WriteTargetExecutionAsync(paths, unsupportedResult);
            return new TargetHandle(resolvedBaseUrl, unsupportedResult);
        }

        return await StartProcessAsync(root, manifest, paths, ResolveBaseUrl(manifest, requestedProtocol), options);
    }

    public static async Task WriteTargetExecutionAsync(ArtifactPaths paths, TargetExecutionResult result)
    {
        await File.WriteAllTextAsync(paths.TargetExecutionJson, ResultJson.Serialize(result));
    }

    private static async Task<TargetHandle> StartProcessAsync(
        string root,
        ImplementationManifest manifest,
        ArtifactPaths paths,
        string resolvedBaseUrl,
        TargetStartOptions options)
    {
        if (string.IsNullOrWhiteSpace(manifest.Executable))
        {
            var result = TargetExecutionResult.UnsupportedResult($"Implementation '{manifest.Id}' does not define a process executable.")
                with { TargetExecutionMode = TargetKinds.Process };
            await WriteTargetExecutionAsync(paths, result);
            return new TargetHandle(manifest.BaseUrl, result);
        }

        if (string.IsNullOrWhiteSpace(resolvedBaseUrl))
        {
            var result = TargetExecutionResult.UnsupportedResult($"Implementation '{manifest.Id}' does not define a base URL.")
                with { TargetExecutionMode = TargetKinds.Process };
            await WriteTargetExecutionAsync(paths, result);
            return new TargetHandle(resolvedBaseUrl, result);
        }

        var startTime = DateTimeOffset.UtcNow;
        var defaultWorkingDirectory = ResolvePath(root, manifest.WorkingDirectory);
        var startCommand = await BuildStartCommandAsync(root, manifest, defaultWorkingDirectory, options.Configuration);
        var workingDirectory = startCommand.WorkingDirectory;
        var startInfo = new ProcessStartInfo(startCommand.Executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in startCommand.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var environment in manifest.Environment)
        {
            startInfo.Environment[environment.Key] = environment.Value;
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start implementation '{manifest.Id}'.");
        var stdoutTask = CopyToFileAsync(process.StandardOutput, paths.TargetStdout, paths.ServerStdout);
        var stderrTask = CopyToFileAsync(process.StandardError, paths.TargetStderr, paths.ServerStderr);
        var commandLine = BuildCommandLine(startCommand.Executable, startCommand.Arguments);

        var startedResult = new TargetExecutionResult
        {
            Status = TargetExecutionStatuses.Started,
            TargetExecutionMode = TargetKinds.Process,
            Started = true,
            StartTimeUtc = startTime,
            ProcessId = process.Id,
            CommandLine = commandLine,
            ExecutablePath = startCommand.Executable,
            WorkingDirectory = workingDirectory,
            StdoutPath = paths.TargetStdout,
            StderrPath = paths.TargetStderr,
            LogsPath = paths.CellDirectory,
            Warnings = startCommand.Warnings
        };

        var readyResult = await WaitReadyAsync(process, manifest, startedResult);
        await WriteTargetExecutionAsync(paths, readyResult);
        return new TargetHandle(resolvedBaseUrl, process, stdoutTask, stderrTask, readyResult);
    }

    private static async Task<TargetHandle> StartDockerAsync(
        string root,
        ImplementationManifest manifest,
        ArtifactPaths paths,
        string resolvedBaseUrl,
        string? requestedProtocol,
        TargetStartOptions options)
    {
        var docker = LoadToolInvoker.ResolveExecutable("docker");
        if (docker is null)
        {
            var result = TargetExecutionResult.UnsupportedResult("Docker executable was not found on PATH. Install/start Docker Desktop or use --target-mode process.")
                with
                {
                    TargetExecutionMode = TargetKinds.Docker,
                    TargetEffectiveBaseUrl = resolvedBaseUrl,
                    StdoutPath = paths.TargetStdout,
                    StderrPath = paths.TargetStderr,
                    LogsPath = paths.CellDirectory
                };
            await WriteTargetExecutionAsync(paths, result);
            return new TargetHandle(resolvedBaseUrl, result);
        }

        var image = options.DockerImageOverride;
        if (string.IsNullOrWhiteSpace(image))
        {
            image = manifest.Image;
        }

        if (string.IsNullOrWhiteSpace(image))
        {
            var result = TargetExecutionResult.UnsupportedResult($"Implementation '{manifest.Id}' does not define a Docker target image.")
                with
                {
                    TargetExecutionMode = TargetKinds.Docker,
                    TargetEffectiveBaseUrl = resolvedBaseUrl,
                    StdoutPath = paths.TargetStdout,
                    StderrPath = paths.TargetStderr,
                    LogsPath = paths.CellDirectory
                };
            await WriteTargetExecutionAsync(paths, result);
            return new TargetHandle(resolvedBaseUrl, result);
        }

        if (options.BuildDockerImage)
        {
            var buildResult = await BuildDockerImageAsync(root, docker, manifest, image, paths);
            if (buildResult.ExitCode != 0)
            {
                await File.AppendAllTextAsync(paths.TargetStdout, buildResult.Stdout);
                await File.AppendAllTextAsync(paths.TargetStderr, buildResult.Stderr);
                var result = TargetExecutionResult.UnsupportedResult($"Docker target image build failed for '{image}'. See target stdout/stderr artifacts.")
                    with
                    {
                        TargetExecutionMode = TargetKinds.Docker,
                        TargetDockerImage = image,
                        TargetEffectiveBaseUrl = resolvedBaseUrl,
                        CommandLine = buildResult.CommandLine,
                        StdoutPath = paths.TargetStdout,
                        StderrPath = paths.TargetStderr,
                        LogsPath = paths.CellDirectory
                    };
                await WriteTargetExecutionAsync(paths, result);
                return new TargetHandle(resolvedBaseUrl, result);
            }
        }
        else if (!await DockerImageExistsAsync(docker, image, root))
        {
            var result = TargetExecutionResult.UnsupportedResult($"Docker target image '{image}' was not found locally. Run the matching scripts\\build\\Build-*Image.ps1 script or pass --target-docker-build.")
                with
                {
                    TargetExecutionMode = TargetKinds.Docker,
                    TargetDockerImage = image,
                    TargetEffectiveBaseUrl = resolvedBaseUrl,
                    StdoutPath = paths.TargetStdout,
                    StderrPath = paths.TargetStderr,
                    LogsPath = paths.CellDirectory
                };
            await WriteTargetExecutionAsync(paths, result);
            return new TargetHandle(resolvedBaseUrl, result);
        }

        var startTime = DateTimeOffset.UtcNow;
        var containerName = string.IsNullOrWhiteSpace(manifest.ContainerName)
            ? GenerateContainerName(manifest.Id)
            : manifest.ContainerName;
        var networkMode = string.IsNullOrWhiteSpace(options.NetworkMode)
            ? string.IsNullOrWhiteSpace(manifest.DockerNetworkMode) ? TargetNetworkModes.PublishedPort : manifest.DockerNetworkMode
            : options.NetworkMode;
        var networkAlias = GenerateNetworkAlias(manifest.Id);
        var dockerNetwork = ResolveDockerNetworkName(manifest, options, networkMode);
        var networkGenerated = string.Equals(networkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(manifest.DockerNetwork);
        var resourceLimits = DockerResourceControl.Merge(manifest.TargetDockerResourceLimits, options.DockerResourceLimits);
        string? dockerNetworkId = null;
        var networkWarnings = new List<string>();
        if (string.Equals(networkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase))
        {
            var network = await EnsureDockerNetworkAsync(docker, dockerNetwork, networkGenerated, root, paths);
            dockerNetworkId = network.NetworkId;
            networkWarnings.AddRange(network.Warnings);
            if (network.ExitCode != 0)
            {
                var result = TargetExecutionResult.UnsupportedResult($"Docker network '{dockerNetwork}' could not be created or inspected. See docker-network-command.txt and docker-network-inspect.json.")
                    with
                    {
                        TargetExecutionMode = TargetKinds.Docker,
                        TargetDockerImage = image,
                        TargetContainerName = containerName,
                        TargetDockerNetwork = dockerNetwork,
                        TargetDockerNetworkName = dockerNetwork,
                        TargetDockerNetworkMode = networkMode,
                        TargetDockerNetworkGenerated = networkGenerated,
                        TargetEffectiveBaseUrl = resolvedBaseUrl,
                        StdoutPath = paths.TargetStdout,
                        StderrPath = paths.TargetStderr,
                        LogsPath = paths.CellDirectory,
                        NetworkWarnings = networkWarnings,
                        Warnings = networkWarnings
                    };
                await WriteTargetExecutionAsync(paths, result);
                return new TargetHandle(resolvedBaseUrl, result);
            }
        }

        var runArguments = BuildDockerRunArguments(manifest, image, containerName, networkMode, dockerNetwork, networkAlias, resourceLimits);
        var commandLine = BuildCommandLine(docker, runArguments);
        await File.WriteAllTextAsync(paths.TargetDockerCommandTxt, commandLine);

        var run = await RunProcessForOutputAsync(docker, runArguments, root);
        await File.AppendAllTextAsync(paths.TargetStdout, run.Stdout);
        await File.AppendAllTextAsync(paths.ServerStdout, run.Stdout);
        await File.AppendAllTextAsync(paths.TargetStderr, run.Stderr);
        await File.AppendAllTextAsync(paths.ServerStderr, run.Stderr);

        var portMetadata = BuildPortMetadata(manifest);
        var warnings = new List<string> { BenchmarkEvidenceReasons.DockerTargetLocal };
        if (image.EndsWith(":local", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(BenchmarkEvidenceReasons.DockerTargetImageLocalTag);
        }

        if (string.Equals(networkMode, TargetNetworkModes.PublishedPort, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(BenchmarkEvidenceReasons.HostPublishedPort);
            warnings.Add(BenchmarkEvidenceReasons.DockerNetworkSharedHost);
        }
        else if (string.Equals(networkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(BenchmarkEvidenceReasons.SharedDockerNetwork);
            warnings.Add(BenchmarkEvidenceReasons.DockerNetworkLocal);
            warnings.Add(networkGenerated
                ? BenchmarkEvidenceReasons.DockerNetworkGenerated
                : "docker-network-reused");
            warnings.Add(BenchmarkEvidenceReasons.TargetHostPortStillPublishedForValidation);
            warnings.Add(BenchmarkEvidenceReasons.CertificateSniConnectToRouting);
        }
        warnings.AddRange(networkWarnings);

        await CaptureDockerInspectAsync(docker, containerName, root, paths.TargetDockerInspectJson);
        var resourceLimitWarnings = new List<string>();
        var effectiveResourceLimits = DockerResourceControl.ParseEffectiveLimitsFromInspectFile(paths.TargetDockerInspectJson, resourceLimitWarnings);
        resourceLimitWarnings.AddRange(DockerResourceControl.BuildWarnings(resourceLimits, effectiveResourceLimits, targetContainer: true));
        warnings.AddRange(resourceLimitWarnings);

        var startedResult = new TargetExecutionResult
        {
            Status = TargetExecutionStatuses.Started,
            TargetExecutionMode = TargetKinds.Docker,
            Started = run.ExitCode == 0,
            Failed = run.ExitCode != 0,
            StartTimeUtc = startTime,
            CommandLine = commandLine,
            ExecutablePath = docker,
            WorkingDirectory = root,
            StdoutPath = paths.TargetStdout,
            StderrPath = paths.TargetStderr,
            LogsPath = paths.CellDirectory,
            TargetDockerImage = image,
            TargetContainerName = containerName,
            TargetDockerNetwork = dockerNetwork,
            TargetDockerNetworkName = dockerNetwork,
            TargetDockerNetworkId = dockerNetworkId,
            TargetDockerNetworkMode = networkMode,
            TargetNetworkAliases = string.Equals(networkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase)
                ? [networkAlias]
                : [],
            TargetPublishedPorts = portMetadata.Published,
            TargetInternalPorts = portMetadata.Internal,
            TargetEffectiveBaseUrl = resolvedBaseUrl,
            HostRewriteMode = string.Equals(networkMode, TargetNetworkModes.PublishedPort, StringComparison.OrdinalIgnoreCase)
                ? "host-published-port"
                : networkMode,
            TargetDockerCommandLine = commandLine,
            TargetDockerInspectPath = paths.TargetDockerInspectJson,
            TargetDockerNetworkInspectPath = string.Equals(networkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase)
                ? paths.TargetDockerNetworkInspectJson
                : null,
            TargetDockerResourceLimitsRequested = resourceLimits,
            TargetDockerResourceLimitsEffective = effectiveResourceLimits,
            ResourceLimitWarnings = resourceLimitWarnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            TargetDockerNetworkGenerated = networkGenerated,
            NetworkCleanupStatus = networkGenerated ? "pending" : null,
            NetworkWarnings = networkWarnings,
            Errors = run.ExitCode == 0
                ? []
                : new[] { $"docker run exited with code {run.ExitCode}.", run.Stderr.Trim() }
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .ToList(),
            Warnings = warnings
        };

        if (string.Equals(networkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(dockerNetwork))
        {
            await CaptureDockerNetworkInspectAsync(docker, dockerNetwork, root, paths.DockerNetworkInspectJson, paths.TargetDockerNetworkInspectJson);
        }

        if (run.ExitCode != 0)
        {
            var failedResult = await CleanupGeneratedNetworkIfNeededAsync(docker, root, paths, startedResult);
            await WriteTargetExecutionAsync(paths, failedResult);
            return new TargetHandle(resolvedBaseUrl, failedResult);
        }

        var proofResult = await RunDockerCapabilityProofAsync(docker, containerName, root, manifest, paths, startedResult);
        if (proofResult.Failed)
        {
            var stoppedResult = await StopDockerTargetAsync(docker, containerName, paths, proofResult);
            await WriteTargetExecutionAsync(paths, stoppedResult);
            return new TargetHandle(resolvedBaseUrl, stoppedResult);
        }

        var readyResult = await WaitDockerReadyAsync(docker, containerName, root, manifest, resolvedBaseUrl, requestedProtocol, proofResult);
        await CaptureDockerInspectAsync(docker, containerName, root, paths.TargetDockerInspectJson);
        var readyResourceWarnings = readyResult.ResourceLimitWarnings.ToList();
        var readyEffectiveResourceLimits = DockerResourceControl.ParseEffectiveLimitsFromInspectFile(paths.TargetDockerInspectJson, readyResourceWarnings);
        readyResult = readyResult with
        {
            TargetDockerResourceLimitsEffective = readyEffectiveResourceLimits ?? readyResult.TargetDockerResourceLimitsEffective,
            ResourceLimitWarnings = readyResourceWarnings
                .Concat(DockerResourceControl.BuildWarnings(resourceLimits, readyEffectiveResourceLimits ?? readyResult.TargetDockerResourceLimitsEffective, targetContainer: true))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Warnings = readyResult.Warnings
                .Concat(readyResourceWarnings)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
        if (string.Equals(networkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(dockerNetwork))
        {
            await CaptureDockerNetworkInspectAsync(docker, dockerNetwork, root, paths.DockerNetworkInspectJson, paths.TargetDockerNetworkInspectJson);
        }
        await WriteTargetExecutionAsync(paths, readyResult);
        return new TargetHandle(resolvedBaseUrl, docker, containerName, paths, readyResult);
    }

    private static async Task<TargetStartCommand> BuildStartCommandAsync(
        string root,
        ImplementationManifest manifest,
        string defaultWorkingDirectory,
        string? configuration)
    {
        if (!string.IsNullOrWhiteSpace(manifest.Project) &&
            string.Equals(manifest.Executable, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var projectPath = ResolvePath(root, manifest.Project);
            var projectDirectory = Path.GetDirectoryName(projectPath) ?? defaultWorkingDirectory;
            var workingDirectory = string.IsNullOrWhiteSpace(manifest.WorkingDirectory) || manifest.WorkingDirectory == "."
                ? projectDirectory
                : defaultWorkingDirectory;
            var normalizedConfiguration = string.IsNullOrWhiteSpace(configuration) ? null : configuration.Trim();
            var warnings = new List<string>();
            if (!string.IsNullOrWhiteSpace(normalizedConfiguration))
            {
                warnings.Add($"target-configuration:{normalizedConfiguration}");
            }

            var targetPath = await TryResolveProjectTargetPathAsync(projectPath, warnings, normalizedConfiguration);
            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                if (!File.Exists(targetPath))
                {
                    await TryBuildProjectAsync(projectPath, warnings, normalizedConfiguration);
                }

                if (File.Exists(targetPath))
                {
                    return new TargetStartCommand(
                        manifest.Executable,
                        ["exec", targetPath, .. NormalizeDirectAppArguments(manifest.CommandArguments)],
                        workingDirectory,
                        warnings);
                }
            }

            warnings.Add("target-direct-dll-startup-unavailable; falling back to dotnet run wrapper.");
            var runArguments = new List<string>
            {
                "run",
                "--no-launch-profile",
                "--project",
                projectPath
            };
            if (!string.IsNullOrWhiteSpace(normalizedConfiguration))
            {
                runArguments.Add("--configuration");
                runArguments.Add(normalizedConfiguration);
            }

            runArguments.AddRange(manifest.CommandArguments);
            return new TargetStartCommand(
                manifest.Executable,
                runArguments,
                defaultWorkingDirectory,
                warnings);
        }

        return new TargetStartCommand(manifest.Executable, manifest.CommandArguments, defaultWorkingDirectory, []);
    }

    private static IReadOnlyList<string> BuildDockerRunArguments(
        ImplementationManifest manifest,
        string image,
        string containerName,
        string networkMode,
        string? dockerNetwork,
        string? networkAlias,
        DockerResourceLimits? resourceLimits = null)
    {
        var arguments = new List<string>
        {
            "run",
            "--detach",
            "--name",
            containerName,
            "--label",
            "incursa.protocol-lab.target=true",
            "--label",
            $"incursa.protocol-lab.implementation={manifest.Id}"
        };

        if (string.Equals(networkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(dockerNetwork))
        {
            arguments.Add("--network");
            arguments.Add(dockerNetwork);
            if (!string.IsNullOrWhiteSpace(networkAlias))
            {
            arguments.Add("--network-alias");
                arguments.Add(networkAlias);
            }
        }

        DockerResourceControl.AddDockerRunArguments(arguments, resourceLimits);

        if (string.Equals(networkMode, TargetNetworkModes.PublishedPort, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(networkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(networkMode))
        {
            foreach (var port in manifest.Ports)
            {
                foreach (var protocol in ExpandPortProtocols(port.Protocol))
                {
                    var hostPort = port.HostPort ?? port.ContainerPort;
                    arguments.Add("--publish");
                    arguments.Add($"{hostPort.ToString(CultureInfo.InvariantCulture)}:{port.ContainerPort.ToString(CultureInfo.InvariantCulture)}/{protocol}");
                }
            }
        }

        foreach (var environment in manifest.Environment.Concat(manifest.DockerEnvironment))
        {
            arguments.Add("--env");
            arguments.Add($"{environment.Key}={environment.Value}");
        }

        arguments.Add(image);
        arguments.AddRange(manifest.DockerCommandArguments.Count > 0 ? manifest.DockerCommandArguments : manifest.CommandArguments);
        return arguments;
    }

    internal static string BuildDockerRunCommandLineForTest(
        ImplementationManifest manifest,
        string image,
        string containerName,
        string networkMode,
        string? dockerNetwork,
        DockerResourceLimits? resourceLimits = null)
    {
        return BuildCommandLine("docker", BuildDockerRunArguments(manifest, image, containerName, networkMode, dockerNetwork, GenerateNetworkAlias(manifest.Id), resourceLimits));
    }

    private static async Task<ProcessOutput> BuildDockerImageAsync(
        string root,
        string docker,
        ImplementationManifest manifest,
        string image,
        ArtifactPaths paths)
    {
        if (string.IsNullOrWhiteSpace(manifest.Dockerfile))
        {
            return new ProcessOutput(1, "", $"Implementation '{manifest.Id}' does not define dockerfile.");
        }

        var dockerfile = ResolvePath(root, manifest.Dockerfile);
        var context = ResolvePath(root, string.IsNullOrWhiteSpace(manifest.BuildContext) ? "." : manifest.BuildContext);
        var arguments = new List<string>
        {
            "build",
            "--tag",
            image,
            "--file",
            dockerfile,
            context
        };
        var commandLine = BuildCommandLine(docker, arguments);
        await File.WriteAllTextAsync(paths.TargetDockerCommandTxt, commandLine);
        var result = await RunProcessForOutputAsync(docker, arguments, root);
        return result with { CommandLine = commandLine };
    }

    private static string ResolveDockerNetworkName(ImplementationManifest manifest, TargetStartOptions options, string networkMode)
    {
        if (!string.Equals(networkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(manifest.DockerNetwork) ? "" : manifest.DockerNetwork;
        }

        if (!string.IsNullOrWhiteSpace(manifest.DockerNetwork))
        {
            return manifest.DockerNetwork;
        }

        if (!string.IsNullOrWhiteSpace(options.DockerNetworkName))
        {
            return options.DockerNetworkName;
        }

        return GenerateDockerNetworkName("local");
    }

    internal static string GenerateDockerNetworkName(string runId)
    {
        var sanitized = new string(runId
            .Select(static character => char.IsAsciiLetterOrDigit(character) || character == '-' || character == '_' ? char.ToLowerInvariant(character) : '-')
            .ToArray())
            .Trim('-');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "run";
        }

        var value = $"protocol-lab-{sanitized}";
        return value[..Math.Min(63, value.Length)];
    }

    internal static string GenerateNetworkAlias(string implementationId)
    {
        var sanitized = new string(implementationId
            .Select(static character => char.IsAsciiLetterOrDigit(character) || character == '-' ? char.ToLowerInvariant(character) : '-')
            .ToArray())
            .Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "target" : sanitized[..Math.Min(63, sanitized.Length)];
    }

    private static async Task<DockerNetworkResult> EnsureDockerNetworkAsync(
        string docker,
        string networkName,
        bool generated,
        string root,
        ArtifactPaths paths)
    {
        var warnings = new List<string>();
        var inspect = await RunProcessForOutputAsync(docker, ["network", "inspect", networkName], root);
        if (inspect.ExitCode != 0)
        {
            var createArguments = new[] { "network", "create", "--driver", "bridge", "--label", "incursa.protocol-lab.network=true", networkName };
            var createCommandLine = BuildCommandLine(docker, createArguments);
            await File.WriteAllTextAsync(paths.DockerNetworkCommandTxt, createCommandLine);
            var create = await RunProcessForOutputAsync(docker, createArguments, root);
            await File.AppendAllTextAsync(paths.DockerNetworkCleanupTxt, create.Stdout + create.Stderr);
            if (create.ExitCode != 0)
            {
                warnings.Add(BenchmarkEvidenceReasons.DockerNetworkCleanupFailed);
                warnings.Add($"docker-network-create-failed: {create.Stderr.Trim()}");
                await File.WriteAllTextAsync(paths.DockerNetworkInspectJson, inspect.Stderr);
                await File.WriteAllTextAsync(paths.TargetDockerNetworkInspectJson, inspect.Stderr);
                return new DockerNetworkResult(create.ExitCode, null, warnings);
            }
        }
        else
        {
            await File.WriteAllTextAsync(paths.DockerNetworkCommandTxt, $"docker network inspect {networkName}");
            if (generated)
            {
                warnings.Add("docker-network-generated-already-existed");
            }
        }

        var finalInspect = await RunProcessForOutputAsync(docker, ["network", "inspect", networkName], root);
        var inspectText = finalInspect.ExitCode == 0 ? finalInspect.Stdout : finalInspect.Stderr;
        await File.WriteAllTextAsync(paths.DockerNetworkInspectJson, inspectText);
        await File.WriteAllTextAsync(paths.TargetDockerNetworkInspectJson, inspectText);

        var idResult = await RunProcessForOutputAsync(docker, ["network", "inspect", "--format", "{{.Id}}", networkName], root);
        var networkId = idResult.ExitCode == 0 ? idResult.Stdout.Trim() : null;
        if (finalInspect.ExitCode != 0)
        {
            warnings.Add($"docker-network-inspect-failed: {finalInspect.Stderr.Trim()}");
        }

        return new DockerNetworkResult(finalInspect.ExitCode, networkId, warnings);
    }

    private static async Task<bool> DockerImageExistsAsync(string docker, string image, string root)
    {
        var result = await RunProcessForOutputAsync(docker, ["image", "inspect", image], root);
        return result.ExitCode == 0;
    }

    private static async Task CaptureDockerInspectAsync(string docker, string containerName, string root, string path)
    {
        var inspect = await RunProcessForOutputAsync(docker, ["inspect", containerName], root);
        await File.WriteAllTextAsync(path, inspect.ExitCode == 0 ? inspect.Stdout : inspect.Stderr);
    }

    private static async Task CaptureDockerNetworkInspectAsync(string docker, string networkName, string root, string networkPath, string targetNetworkPath)
    {
        var inspect = await RunProcessForOutputAsync(docker, ["network", "inspect", networkName], root);
        var text = inspect.ExitCode == 0 ? inspect.Stdout : inspect.Stderr;
        await File.WriteAllTextAsync(networkPath, text);
        await File.WriteAllTextAsync(targetNetworkPath, text);
    }

    private static async Task<TargetExecutionResult> RunDockerCapabilityProofAsync(
        string docker,
        string containerName,
        string root,
        ImplementationManifest manifest,
        ArtifactPaths paths,
        TargetExecutionResult startedResult)
    {
        var proof = manifest.TargetCapabilityProof;
        if (proof is null ||
            proof.DockerExecArguments.Count == 0)
        {
            return startedResult;
        }

        var arguments = new[] { "exec", containerName }.Concat(proof.DockerExecArguments).ToArray();
        var commandLine = BuildCommandLine(docker, arguments);
        var result = await RunProcessForOutputAsync(docker, arguments, root);
        var output = result.Stdout + result.Stderr;
        await File.AppendAllTextAsync(paths.TargetStdout, result.Stdout);
        await File.AppendAllTextAsync(paths.ServerStdout, result.Stdout);
        await File.AppendAllTextAsync(paths.TargetStderr, result.Stderr);
        await File.AppendAllTextAsync(paths.ServerStderr, result.Stderr);

        var warnings = startedResult.TargetCapabilityProofWarnings.ToList();
        var status = "passed";
        if (result.ExitCode != 0)
        {
            status = "failed";
            warnings.Add($"target-capability-proof-exit-code:{result.ExitCode.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(proof.ExpectedOutputContains) &&
            !output.Contains(proof.ExpectedOutputContains, StringComparison.OrdinalIgnoreCase))
        {
            status = "failed";
            warnings.Add($"target-capability-proof-missing-output:{proof.ExpectedOutputContains}");
        }

        var updated = startedResult with
        {
            TargetCapabilityProofId = proof.Id,
            TargetCapabilityProofStatus = status,
            TargetCapabilityProofCommandLine = commandLine,
            TargetCapabilityProofExpectedOutput = proof.ExpectedOutputContains,
            TargetCapabilityProofOutputPath = paths.TargetStderr,
            TargetCapabilityProofWarnings = warnings
                .Where(static warning => !string.IsNullOrWhiteSpace(warning))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        return string.Equals(status, "passed", StringComparison.OrdinalIgnoreCase) || !proof.Required
            ? updated
            : MarkFailed(updated, $"Required target capability proof '{proof.Id}' failed. Expected output: {proof.ExpectedOutputContains}", result.ExitCode);
    }

    internal static async Task<TargetExecutionResult> StopDockerTargetAsync(
        string docker,
        string containerName,
        ArtifactPaths paths,
        TargetExecutionResult result)
    {
        var root = Path.GetPathRoot(paths.CellDirectory) ?? Directory.GetCurrentDirectory();
        var logs = await RunProcessForOutputAsync(docker, ["logs", "--timestamps", containerName], root);
        await File.AppendAllTextAsync(paths.TargetStdout, logs.Stdout);
        await File.AppendAllTextAsync(paths.ServerStdout, logs.Stdout);
        await File.AppendAllTextAsync(paths.TargetStderr, logs.Stderr);
        await File.AppendAllTextAsync(paths.ServerStderr, logs.Stderr);

        await CaptureDockerInspectAsync(docker, containerName, root, paths.TargetDockerInspectJson);
        var stop = await RunProcessForOutputAsync(docker, ["stop", "--time", "10", containerName], root);
        var remove = await RunProcessForOutputAsync(docker, ["rm", "--force", containerName], root);
        var warnings = result.Warnings.ToList();
        var networkWarnings = result.NetworkWarnings.ToList();
        var cleanupStatus = result.NetworkCleanupStatus;
        var cleanupSummary = result.CleanupSummary ?? new DockerCleanupSummary();
        if (logs.ExitCode != 0)
        {
            warnings.Add($"docker logs exited with code {logs.ExitCode}.");
        }

        if (stop.ExitCode != 0)
        {
            warnings.Add($"docker stop exited with code {stop.ExitCode}: {stop.Stderr.Trim()}");
        }

        if (remove.ExitCode != 0)
        {
            warnings.Add($"docker rm exited with code {remove.ExitCode}: {remove.Stderr.Trim()}");
        }
        cleanupSummary = cleanupSummary with
        {
            TargetContainerCleanupAttempted = true,
            TargetContainerCleanupSucceeded = stop.ExitCode == 0 && remove.ExitCode == 0,
            TargetContainerName = containerName,
            Errors = cleanupSummary.Errors
                .Concat(stop.ExitCode == 0 ? [] : [$"docker stop exited with code {stop.ExitCode}: {stop.Stderr.Trim()}"])
                .Concat(remove.ExitCode == 0 ? [] : [$"docker rm exited with code {remove.ExitCode}: {remove.Stderr.Trim()}"])
                .ToArray()
        };

        if (result.TargetDockerNetworkGenerated &&
            !string.IsNullOrWhiteSpace(result.TargetDockerNetworkName))
        {
            var cleanup = await RunProcessForOutputAsync(docker, ["network", "rm", result.TargetDockerNetworkName], root);
            await File.WriteAllTextAsync(
                paths.DockerNetworkCleanupTxt,
                BuildCommandLine(docker, ["network", "rm", result.TargetDockerNetworkName]) + Environment.NewLine + cleanup.Stdout + cleanup.Stderr);
            if (cleanup.ExitCode == 0)
            {
                cleanupStatus = "removed";
                cleanupSummary = cleanupSummary with
                {
                    NetworkCleanupAttempted = true,
                    NetworkCleanupSucceeded = true,
                    NetworkName = result.TargetDockerNetworkName
                };
            }
            else
            {
                cleanupStatus = "failed";
                warnings.Add(BenchmarkEvidenceReasons.DockerNetworkCleanupFailed);
                networkWarnings.Add(BenchmarkEvidenceReasons.DockerNetworkCleanupFailed);
                networkWarnings.Add($"docker-network-cleanup-failed: {cleanup.Stderr.Trim()}");
                cleanupSummary = cleanupSummary with
                {
                    NetworkCleanupAttempted = true,
                    NetworkCleanupSucceeded = false,
                    NetworkName = result.TargetDockerNetworkName,
                    Errors = cleanupSummary.Errors.Concat([$"docker-network-cleanup-failed: {cleanup.Stderr.Trim()}"]).ToArray()
                };
            }
        }

        await File.WriteAllTextAsync(paths.DockerCleanupJson, ResultJson.Serialize(cleanupSummary));
        return result with
        {
            ExitCode = stop.ExitCode == 0 ? 0 : result.ExitCode,
            CleanupSummary = cleanupSummary,
            NetworkCleanupStatus = cleanupStatus,
            NetworkWarnings = networkWarnings.Where(static warning => !string.IsNullOrWhiteSpace(warning)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings = warnings.Where(static warning => !string.IsNullOrWhiteSpace(warning)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static async Task<TargetExecutionResult> CleanupGeneratedNetworkIfNeededAsync(
        string docker,
        string root,
        ArtifactPaths paths,
        TargetExecutionResult result)
    {
        if (!result.TargetDockerNetworkGenerated ||
            string.IsNullOrWhiteSpace(result.TargetDockerNetworkName))
        {
            return result;
        }

        var cleanup = await RunProcessForOutputAsync(docker, ["network", "rm", result.TargetDockerNetworkName], root);
        await File.WriteAllTextAsync(
            paths.DockerNetworkCleanupTxt,
            BuildCommandLine(docker, ["network", "rm", result.TargetDockerNetworkName]) + Environment.NewLine + cleanup.Stdout + cleanup.Stderr);

        var warnings = result.Warnings.ToList();
        var networkWarnings = result.NetworkWarnings.ToList();
        if (cleanup.ExitCode == 0)
        {
            var cleanupSummary = result.CleanupSummary ?? new DockerCleanupSummary();
            cleanupSummary = cleanupSummary with
            {
                NetworkCleanupAttempted = true,
                NetworkCleanupSucceeded = true,
                NetworkName = result.TargetDockerNetworkName
            };
            await File.WriteAllTextAsync(paths.DockerCleanupJson, ResultJson.Serialize(cleanupSummary));
            return result with
            {
                NetworkCleanupStatus = "removed",
                CleanupSummary = cleanupSummary,
                Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        warnings.Add(BenchmarkEvidenceReasons.DockerNetworkCleanupFailed);
        networkWarnings.Add(BenchmarkEvidenceReasons.DockerNetworkCleanupFailed);
        networkWarnings.Add($"docker-network-cleanup-failed: {cleanup.Stderr.Trim()}");
        var failedCleanupSummary = result.CleanupSummary ?? new DockerCleanupSummary();
        failedCleanupSummary = failedCleanupSummary with
        {
            NetworkCleanupAttempted = true,
            NetworkCleanupSucceeded = false,
            NetworkName = result.TargetDockerNetworkName,
            Errors = failedCleanupSummary.Errors.Concat([$"docker-network-cleanup-failed: {cleanup.Stderr.Trim()}"]).ToArray()
        };
        await File.WriteAllTextAsync(paths.DockerCleanupJson, ResultJson.Serialize(failedCleanupSummary));
        return result with
        {
            NetworkCleanupStatus = "failed",
            CleanupSummary = failedCleanupSummary,
            NetworkWarnings = networkWarnings.Where(static warning => !string.IsNullOrWhiteSpace(warning)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings = warnings.Where(static warning => !string.IsNullOrWhiteSpace(warning)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static async Task<TargetExecutionResult> WaitDockerReadyAsync(
        string docker,
        string containerName,
        string root,
        ImplementationManifest manifest,
        string resolvedBaseUrl,
        string? requestedProtocol,
        TargetExecutionResult startedResult)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(1, manifest.ReadinessCheck.TimeoutSeconds));
        var deadline = DateTimeOffset.UtcNow + timeout;
        var errors = new List<string>();

        if (manifest.ReadinessCheck.StartupDelayMilliseconds > 0)
        {
            await Task.Delay(manifest.ReadinessCheck.StartupDelayMilliseconds);
        }

        while (DateTimeOffset.UtcNow < deadline)
        {
            var running = await RunProcessForOutputAsync(docker, ["inspect", "--format", "{{.State.Running}}", containerName], root);
            if (running.ExitCode != 0)
            {
                errors.Add(running.Stderr.Trim());
                await Task.Delay(250);
                continue;
            }

            if (!running.Stdout.Contains("true", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Docker container is not running.");
                await Task.Delay(250);
                continue;
            }

            try
            {
                if (string.Equals(requestedProtocol, "h3", StringComparison.OrdinalIgnoreCase))
                {
                    if (await IsManagedHttp3ReadyAsync(resolvedBaseUrl, manifest.ReadinessCheck.Url, manifest.CertificateMode))
                    {
                        return MarkReady(startedResult);
                    }

                    errors.Add("Managed HTTP/3 readiness probe did not receive a successful HTTP/3 response.");
                }
                else if (string.Equals(manifest.ReadinessCheck.Type, ReadinessCheckTypes.Http, StringComparison.OrdinalIgnoreCase) &&
                         await IsHttpReadyAsync(resolvedBaseUrl, manifest.ReadinessCheck.Url))
                {
                    return MarkReady(startedResult);
                }
                else if (string.Equals(manifest.ReadinessCheck.Type, ReadinessCheckTypes.Tcp, StringComparison.OrdinalIgnoreCase) &&
                         await IsTcpReadyAsync(resolvedBaseUrl))
                {
                    return MarkReady(startedResult);
                }
                else if (string.Equals(manifest.ReadinessCheck.Type, ReadinessCheckTypes.None, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(manifest.ReadinessCheck.Type, ReadinessCheckTypes.ProcessStarted, StringComparison.OrdinalIgnoreCase))
                {
                    return MarkReady(startedResult);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException or UriFormatException or InvalidOperationException)
            {
                errors.Add(ex.Message);
            }

            await Task.Delay(250);
        }

        var error = errors.Count == 0
            ? $"Docker target did not become ready within {timeout.TotalSeconds:0} seconds."
            : $"Docker target did not become ready within {timeout.TotalSeconds:0} seconds. Last error: {errors[^1]}";
        return MarkFailed(startedResult, error, null);
    }

    private static async Task<bool> IsManagedHttp3ReadyAsync(string baseUrl, string readinessPath, string certificateMode)
    {
        var uri = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), readinessPath.TrimStart('/'));
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(2),
            SslOptions = new SslClientAuthenticationOptions()
        };
        if (ShouldBypassCertificateValidation(uri, certificateMode))
        {
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
        }

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, uri)
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        using var response = await client.SendAsync(request);
        return response.IsSuccessStatusCode && response.Version.Major == 3;
    }

    private static bool ShouldBypassCertificateValidation(Uri uri, string certificateMode)
    {
        if (!IPAddress.TryParse(uri.Host, out var address) && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return certificateMode.Contains("development", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("local", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("loopback", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("self-signed", StringComparison.OrdinalIgnoreCase) ||
            certificateMode.Contains("bypass", StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateContainerName(string implementationId)
    {
        var value = $"protocol-lab-{ArtifactLayout.SanitizeSegment(implementationId).ToLowerInvariant()}-{Guid.NewGuid():N}";
        return value[..Math.Min(63, value.Length)];
    }

    private static (Dictionary<string, string> Published, Dictionary<string, string> Internal) BuildPortMetadata(ImplementationManifest manifest)
    {
        var published = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var internalPorts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var port in manifest.Ports)
        {
            foreach (var protocol in ExpandPortProtocols(port.Protocol))
            {
                var key = $"{port.Name}/{protocol}";
                var hostPort = port.HostPort ?? port.ContainerPort;
                published[key] = $"{hostPort.ToString(CultureInfo.InvariantCulture)}->{port.ContainerPort.ToString(CultureInfo.InvariantCulture)}/{protocol}";
                internalPorts[key] = $"{port.ContainerPort.ToString(CultureInfo.InvariantCulture)}/{protocol}";
            }
        }

        return (published, internalPorts);
    }

    private static IEnumerable<string> ExpandPortProtocols(string protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            yield return "tcp";
            yield break;
        }

        foreach (var candidate in protocol.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            yield return candidate;
        }
    }

    private static async Task<string?> TryResolveProjectTargetPathAsync(string projectPath, List<string> warnings, string? configuration)
    {
        try
        {
            var arguments = new List<string>
            {
                "msbuild",
                projectPath,
                "-getProperty:TargetPath",
                "-nologo"
            };
            if (!string.IsNullOrWhiteSpace(configuration))
            {
                arguments.Add($"-property:Configuration={configuration}");
            }

            var result = await RunProcessForOutputAsync(
                "dotnet",
                arguments,
                Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory());
            if (result.ExitCode != 0)
            {
                warnings.Add($"target-path-resolution-failed: dotnet msbuild exited {result.ExitCode}.");
                return null;
            }

            return result.Stdout
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault(line => line.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            warnings.Add($"target-path-resolution-failed: {ex.Message}");
            return null;
        }
    }

    private static async Task TryBuildProjectAsync(string projectPath, List<string> warnings, string? configuration)
    {
        try
        {
            var arguments = new List<string>
            {
                "build",
                projectPath,
                "--nologo",
                "--verbosity",
                "minimal"
            };
            if (!string.IsNullOrWhiteSpace(configuration))
            {
                arguments.Add("--configuration");
                arguments.Add(configuration);
            }

            var result = await RunProcessForOutputAsync(
                "dotnet",
                arguments,
                Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory());
            if (result.ExitCode != 0)
            {
                warnings.Add($"target-build-before-direct-start-failed: dotnet build exited {result.ExitCode}.");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            warnings.Add($"target-build-before-direct-start-failed: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> NormalizeDirectAppArguments(IReadOnlyList<string> arguments)
    {
        return arguments.Count > 0 && string.Equals(arguments[0], "--", StringComparison.Ordinal)
            ? arguments.Skip(1).ToArray()
            : arguments;
    }

    private static async Task<ProcessOutput> RunProcessForOutputAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{executable}'.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessOutput(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string BuildCommandLine(string executable, IReadOnlyList<string> arguments)
    {
        return string.Join(" ", new[] { QuoteArgument(executable) }.Concat(arguments.Select(QuoteArgument)));
    }

    private static string QuoteArgument(string argument)
    {
        return argument.Contains(' ', StringComparison.Ordinal) || argument.Contains('"', StringComparison.Ordinal)
            ? "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : argument;
    }

    private static async Task<TargetExecutionResult> WaitReadyAsync(
        Process process,
        ImplementationManifest manifest,
        TargetExecutionResult startedResult)
    {
        var type = string.IsNullOrWhiteSpace(manifest.ReadinessCheck.Type)
            ? ReadinessCheckTypes.None
            : manifest.ReadinessCheck.Type;

        if (string.Equals(type, ReadinessCheckTypes.None, StringComparison.OrdinalIgnoreCase))
        {
            await DelayIfConfiguredAsync(process, manifest.ReadinessCheck.StartupDelayMilliseconds);
            return MarkReady(startedResult);
        }

        if (string.Equals(type, ReadinessCheckTypes.ProcessStarted, StringComparison.OrdinalIgnoreCase))
        {
            await DelayIfConfiguredAsync(process, manifest.ReadinessCheck.StartupDelayMilliseconds);
            return process.HasExited
                ? MarkFailed(startedResult, $"Target process exited before readiness with code {process.ExitCode}.", process.ExitCode)
                : MarkReady(startedResult);
        }

        var timeout = TimeSpan.FromSeconds(Math.Max(1, manifest.ReadinessCheck.TimeoutSeconds));
        var deadline = DateTimeOffset.UtcNow + timeout;
        var errors = new List<string>();

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                return MarkFailed(startedResult, $"Target process exited before readiness with code {process.ExitCode}.", process.ExitCode);
            }

            try
            {
                if (string.Equals(type, ReadinessCheckTypes.Http, StringComparison.OrdinalIgnoreCase) &&
                    await IsHttpReadyAsync(manifest.BaseUrl, manifest.ReadinessCheck.Url))
                {
                    return MarkReady(startedResult);
                }

                if (string.Equals(type, ReadinessCheckTypes.Tcp, StringComparison.OrdinalIgnoreCase) &&
                    await IsTcpReadyAsync(manifest.BaseUrl))
                {
                    return MarkReady(startedResult);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException or UriFormatException)
            {
                errors.Add(ex.Message);
            }

            await Task.Delay(250);
        }

        var error = errors.Count == 0
            ? $"Target did not become ready within {timeout.TotalSeconds:0} seconds."
            : $"Target did not become ready within {timeout.TotalSeconds:0} seconds. Last error: {errors[^1]}";
        return MarkFailed(startedResult, error, process.HasExited ? process.ExitCode : null);
    }

    private static TargetExecutionResult MarkReady(TargetExecutionResult result)
    {
        return result with
        {
            Status = TargetExecutionStatuses.Ready,
            Ready = true,
            ReadyTimeUtc = DateTimeOffset.UtcNow
        };
    }

    private static TargetExecutionResult MarkFailed(TargetExecutionResult result, string error, int? exitCode)
    {
        return result with
        {
            Status = TargetExecutionStatuses.Failed,
            Failed = true,
            ExitCode = exitCode,
            Errors = [.. result.Errors, error]
        };
    }

    private static async Task<bool> IsHttpReadyAsync(string baseUrl, string readinessPath)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var uri = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), readinessPath.TrimStart('/'));
        using var response = await client.GetAsync(uri);
        return response.IsSuccessStatusCode;
    }

    private static async Task DelayIfConfiguredAsync(Process process, int startupDelayMilliseconds)
    {
        if (startupDelayMilliseconds <= 0)
        {
            return;
        }

        var delay = Task.Delay(startupDelayMilliseconds);
        var exited = process.WaitForExitAsync();
        await Task.WhenAny(delay, exited);
    }

    private static async Task<bool> IsTcpReadyAsync(string baseUrl)
    {
        var uri = new Uri(baseUrl);
        using var client = new TcpClient();
        await client.ConnectAsync(uri.Host, uri.Port);
        return true;
    }

    private static async Task CopyToFileAsync(TextReader reader, string primaryPath, string aliasPath)
    {
        await using var primary = new FileStream(primaryPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var alias = new FileStream(aliasPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var primaryWriter = new StreamWriter(primary);
        await using var aliasWriter = new StreamWriter(alias);

        while (await reader.ReadLineAsync() is { } line)
        {
            await primaryWriter.WriteLineAsync(line);
            await aliasWriter.WriteLineAsync(line);
            await primaryWriter.FlushAsync();
            await aliasWriter.FlushAsync();
        }
    }

    private static string ResolvePath(string root, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == ".")
        {
            return root;
        }

        return Path.IsPathFullyQualified(value)
            ? value
            : Path.GetFullPath(Path.Combine(root, value));
    }

    private static string ResolveRequestedMode(
        ImplementationManifest manifest,
        string? externalBaseUrl,
        TargetStartOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Mode))
        {
            return options.Mode;
        }

        if (!string.IsNullOrWhiteSpace(externalBaseUrl))
        {
            return TargetKinds.External;
        }

        return string.IsNullOrWhiteSpace(manifest.TargetKind)
            ? ""
            : manifest.TargetKind;
    }

    private static string ResolveBaseUrl(ImplementationManifest manifest, string? requestedProtocol, string? targetMode = null)
    {
        if (string.Equals(targetMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(requestedProtocol) &&
            manifest.DockerProtocolBaseUrls.TryGetValue(requestedProtocol, out var dockerProtocolBaseUrl) &&
            !string.IsNullOrWhiteSpace(dockerProtocolBaseUrl))
        {
            return dockerProtocolBaseUrl;
        }

        if (string.Equals(targetMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(manifest.DockerBaseUrl))
        {
            return manifest.DockerBaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(requestedProtocol) &&
            manifest.ProtocolBaseUrls.TryGetValue(requestedProtocol, out var protocolBaseUrl) &&
            !string.IsNullOrWhiteSpace(protocolBaseUrl))
        {
            return protocolBaseUrl;
        }

        return manifest.BaseUrl;
    }

    private static void EnsureTextFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "");
        }
    }

    private sealed record TargetStartCommand(string Executable, IReadOnlyList<string> Arguments, string WorkingDirectory, List<string> Warnings);

    private sealed record DockerNetworkResult(int ExitCode, string? NetworkId, IReadOnlyList<string> Warnings);

    private sealed record ProcessOutput(int ExitCode, string Stdout, string Stderr)
    {
        public string? CommandLine { get; init; }
    }
}

internal sealed record TargetStartOptions(
    string? Mode = null,
    string? NetworkMode = null,
    bool BuildDockerImage = false,
    string? DockerImageOverride = null,
    string? DockerNetworkName = null,
    DockerResourceLimits? DockerResourceLimits = null,
    string? Configuration = null)
{
    public static TargetStartOptions Default { get; } = new();
}
