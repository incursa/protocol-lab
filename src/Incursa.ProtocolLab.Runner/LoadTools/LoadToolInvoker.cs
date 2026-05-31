// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Incursa.ProtocolLab.Model;

namespace Incursa.ProtocolLab.Runner;

internal sealed record LoadToolRun(int ExitCode, string Stdout, string Stderr)
{
    public string? ContainerId { get; init; }
    public string? ContainerName { get; init; }
    public string? DockerInspectPath { get; init; }
    public DockerResourceLimits? DockerResourceLimitsRequested { get; init; }
    public DockerResourceLimits? DockerResourceLimitsEffective { get; init; }
    public IReadOnlyList<string> ResourceLimitWarnings { get; init; } = [];
    public bool DockerMetricsAvailable { get; init; }
    public DockerContainerMetricsSummary? DockerMetricsSummary { get; init; }
    public string? SaturationStatus { get; init; }
    public IReadOnlyList<string> SaturationWarnings { get; init; } = [];
    public Dictionary<string, string> DockerMetricsArtifacts { get; init; } = [];
    public DockerCleanupSummary? CleanupSummary { get; init; }
}
internal sealed record LoadToolParseResult(bool ParsedMetricsAvailable, HttpMetrics Metrics, IReadOnlyList<string> Warnings);
internal sealed record LoadToolExecutionPlan(
    IReadOnlyList<string> Arguments,
    string CommandLine,
    string? DockerCommandLine,
    string WorkingDirectory,
    Uri RequestedTargetUrl,
    Uri TargetUrl,
    string? ConnectTarget,
    string? HostRewriteMode,
    string? Sni,
    string? DockerNetwork,
    string? DockerContainerName,
    DockerResourceLimits? DockerResourceLimits,
    bool CaptureDockerMetrics,
    TimeSpan DockerMetricsInterval,
    string CertificateMode,
    RunCell Cell,
    ArtifactPaths Paths,
    RequestedLoadShape RequestedLoadShape,
    EffectiveLoadShape EffectiveLoadShape,
    LoadShapeSemantics Semantics,
    bool CaptureQlog);

internal sealed record ResolvedLoadTool(
    LoadToolManifest Manifest,
    string Mode,
    string? ExecutablePath,
    string? DockerImage,
    string? Version,
    IReadOnlyList<string> Warnings);

internal sealed record LoadToolResolution(
    ResolvedLoadTool? Tool,
    LoadToolExecutionResult Result)
{
    public bool CanExecute => Tool is not null && Result.Status == LoadToolExecutionStatuses.Succeeded;
}

internal sealed record LoadToolTargetRoute(
    Uri EffectiveUrl,
    string? ConnectTarget,
    string? HostRewriteMode,
    string? DockerNetwork);

internal static class LoadToolInvoker
{
    public static LoadToolResolution Resolve(
        IReadOnlyList<LoadToolManifest> manifests,
        RunCell cell,
        string? requestedTool,
        string? requestedMode)
    {
        var compatible = manifests
            .Where(manifest => IsCompatible(manifest, cell))
            .ToArray();
        if (compatible.Length == 0)
        {
            return Unavailable(
                requestedTool,
                requestedMode,
                $"No load-tool manifest supports protocol '{cell.Protocol}' and scenario family '{cell.Scenario.Family}'.");
        }

        if (!string.IsNullOrWhiteSpace(requestedTool))
        {
            var manifest = manifests.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, requestedTool, StringComparison.OrdinalIgnoreCase));
            if (manifest is null)
            {
                return Unavailable(
                    requestedTool,
                    requestedMode,
                    $"Load tool '{requestedTool}' does not have a manifest under load-tools.");
            }

            var requested = compatible.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, requestedTool, StringComparison.OrdinalIgnoreCase));
            if (requested is null)
            {
                return Unavailable(
                    requestedTool,
                    requestedMode,
                    $"Load tool '{requestedTool}' is not compatible with protocol '{cell.Protocol}' and scenario family '{cell.Scenario.Family}'.");
            }

            return ResolveManifest(requested, requestedMode);
        }

        var unavailableReasons = new List<string>();
        foreach (var manifest in compatible.OrderBy(manifest => GetSelectionPriority(manifest, cell)))
        {
            var resolution = ResolveManifest(manifest, requestedMode);
            if (resolution.CanExecute)
            {
                return resolution;
            }

            unavailableReasons.AddRange(resolution.Result.Errors);
        }

        return Unavailable(
            requestedTool,
            requestedMode,
            unavailableReasons.Count == 0
                ? "No compatible load tool is available."
                : string.Join(" ", unavailableReasons));
    }

    public static string? ResolveExecutable(string name)
    {
        if (Path.IsPathFullyQualified(name) && File.Exists(name))
        {
            return name;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var extensions = GetExecutableExtensions();

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, name + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static async Task<LoadToolRun> RunAsync(ResolvedLoadTool tool, LoadToolExecutionPlan plan)
    {
        if (string.Equals(tool.Mode, LoadToolKinds.Managed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tool.Manifest.Id, ManagedHttp3LoadGenerator.ToolId, StringComparison.OrdinalIgnoreCase))
        {
            return await ManagedHttp3LoadGenerator.RunAsync(plan);
        }

        if (string.Equals(tool.Mode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tool.Mode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            return await RunDockerAsync(tool, plan);
        }

        if ((!string.Equals(tool.Mode, LoadToolKinds.Process, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(tool.Mode, TargetKinds.Process, StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(tool.ExecutablePath))
        {
            return new LoadToolRun(1, "", $"Load tool mode '{tool.Mode}' is not executable in Phase 2B.");
        }

        return await RunProcessAsync(tool.ExecutablePath, plan.Arguments);
    }

    public static LoadToolExecutionPlan BuildExecutionPlan(
        ResolvedLoadTool tool,
        Uri targetUrl,
        RunCell cell,
        ArtifactPaths paths,
        TargetExecutionResult? targetExecution = null,
        DockerResourceLimits? dockerResourceLimits = null,
        bool captureDockerMetrics = false,
        TimeSpan? dockerMetricsInterval = null,
        bool captureQlog = true)
    {
        var route = ResolveTargetRoute(tool, targetUrl, targetExecution);
        var effectiveTargetUrl = route.EffectiveUrl;
        var resourceLimits = DockerResourceControl.Merge(tool.Manifest.LoadToolDockerResourceLimits, dockerResourceLimits);
        var dockerContainerName = IsDockerTool(tool) && (resourceLimits?.HasAnyLimit == true || captureDockerMetrics)
            ? BuildLoadToolContainerName(cell, paths)
            : null;
        var arguments = BuildArguments(tool.Manifest, effectiveTargetUrl, cell, paths, tool.Mode, route.ConnectTarget, captureQlog);
        var requested = new RequestedLoadShape
        {
            Connections = cell.Connections,
            Concurrency = cell.Connections,
            StreamsPerConnection = cell.StreamsPerConnection,
            DurationSeconds = cell.DurationSeconds,
            WarmupSeconds = cell.WarmupSeconds,
            Repetitions = cell.Repetition
        };
        var semantics = BuildLoadShapeSemantics(tool.Manifest, cell);
        var effective = BuildEffectiveLoadShape(tool.Manifest, cell, semantics);
        var executable = string.Equals(tool.Manifest.Id, ManagedHttp3LoadGenerator.ToolId, StringComparison.OrdinalIgnoreCase)
            ? ManagedHttp3LoadGenerator.ToolId
            : tool.ExecutablePath ?? tool.Manifest.Executable;
        return new LoadToolExecutionPlan(
            arguments,
            FormatCommandLine(executable, arguments),
            BuildDockerCommandLine(tool, arguments, paths, route.DockerNetwork, resourceLimits, dockerContainerName),
            Directory.GetCurrentDirectory(),
            targetUrl,
            effectiveTargetUrl,
            route.ConnectTarget,
            route.HostRewriteMode,
            GetSni(tool, targetUrl),
            route.DockerNetwork,
            dockerContainerName,
            resourceLimits,
            captureDockerMetrics && IsDockerTool(tool),
            dockerMetricsInterval ?? TimeSpan.FromSeconds(1),
            cell.Implementation.CertificateMode,
            cell,
            paths,
            requested,
            effective,
            semantics,
            captureQlog);
    }

    public static async Task<string?> CaptureVersionAsync(LoadToolManifest manifest, string executablePath)
    {
        if (string.Equals(manifest.Kind, LoadToolKinds.Managed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(manifest.Id, ManagedHttp3LoadGenerator.ToolId, StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeInformation.FrameworkDescription;
        }

        var arguments = manifest.VersionCommand.Count == 0
            ? ["--version"]
            : manifest.VersionCommand;

        try
        {
            var run = await RunProcessAsync(executablePath, arguments);
            var combined = string.Join(Environment.NewLine, new[] { run.Stdout, run.Stderr }
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(combined)
                ? null
                : combined.Trim();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return $"version unavailable: {ex.Message}";
        }
    }

    public static Task<string?> CaptureVersionAsync(ResolvedLoadTool tool)
    {
        if (string.Equals(tool.Mode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tool.Mode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            var arguments = tool.Manifest.VersionCommand.Count == 0
                ? ["--version"]
                : tool.Manifest.VersionCommand;
            return CaptureDockerVersionAsync(tool, arguments);
        }

        return CaptureVersionAsync(tool.Manifest, tool.ExecutablePath ?? "");
    }

    internal static async Task<(string? ImageId, string? ImageDigest, IReadOnlyList<string> Warnings)> CaptureDockerImageMetadataAsync(ResolvedLoadTool tool)
    {
        var warnings = new List<string>();
        if (!IsDockerTool(tool) || string.IsNullOrWhiteSpace(tool.ExecutablePath) || string.IsNullOrWhiteSpace(tool.DockerImage))
        {
            return (null, null, warnings);
        }

        try
        {
            var run = await RunProcessAsync(tool.ExecutablePath, ["image", "inspect", "--format", "{{.Id}}|{{json .RepoDigests}}", tool.DockerImage]);
            if (run.ExitCode != 0)
            {
                var stderr = string.IsNullOrWhiteSpace(run.Stderr) ? run.Stdout : run.Stderr;
                warnings.Add($"Docker image metadata could not be captured for '{tool.DockerImage}': {FirstLine(stderr) ?? "no output"}");
                return (null, null, warnings);
            }

            var firstLine = FirstLine(run.Stdout);
            if (string.IsNullOrWhiteSpace(firstLine))
            {
                warnings.Add($"Docker image metadata for '{tool.DockerImage}' did not return inspect output.");
                return (null, null, warnings);
            }

            var parts = firstLine.Split('|', 2);
            var imageId = string.IsNullOrWhiteSpace(parts[0]) ? null : parts[0].Trim();
            string? imageDigest = null;
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                try
                {
                    using var document = JsonDocument.Parse(parts[1]);
                    if (document.RootElement.ValueKind == JsonValueKind.Array &&
                        document.RootElement.GetArrayLength() > 0)
                    {
                        imageDigest = document.RootElement[0].GetString();
                    }
                }
                catch (JsonException)
                {
                    warnings.Add($"Docker image digest metadata for '{tool.DockerImage}' was not valid JSON.");
                }
            }

            return (imageId, imageDigest, warnings);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            warnings.Add($"Docker image metadata for '{tool.DockerImage}' could not be captured: {ex.Message}");
            return (null, null, warnings);
        }
    }

    internal static IReadOnlyList<string> BuildArguments(LoadToolManifest manifest, Uri targetUrl, RunCell cell)
    {
        return BuildArguments(manifest, targetUrl, cell, paths: null, mode: manifest.Kind);
    }

    internal static IReadOnlyList<string> BuildArguments(
        LoadToolManifest manifest,
        Uri targetUrl,
        RunCell cell,
        ArtifactPaths? paths,
        string? mode,
        string? connectTarget = null,
        bool captureQlog = true)
    {
        var id = manifest.Id.ToLowerInvariant();
        return id switch
        {
            "h2load" => [.. manifest.DefaultArguments, .. BuildH2loadProtocolArguments(manifest, cell.Protocol, paths, mode, connectTarget, captureQlog), .. BuildH2loadOutputArguments(cell, paths, mode), "-D", cell.DurationSeconds.ToString(CultureInfo.InvariantCulture), "--warm-up-time", cell.WarmupSeconds.ToString(CultureInfo.InvariantCulture), "-c", cell.Connections.ToString(CultureInfo.InvariantCulture), "-m", cell.StreamsPerConnection.ToString(CultureInfo.InvariantCulture), targetUrl.ToString()],
            "oha" => [.. manifest.DefaultArguments, .. BuildOhaProtocolArguments(cell.Protocol), "-z", $"{cell.DurationSeconds.ToString(CultureInfo.InvariantCulture)}s", "-c", cell.Connections.ToString(CultureInfo.InvariantCulture), targetUrl.ToString()],
            ManagedHttp3LoadGenerator.ToolId => ["--http-version", "3.0", "--version-policy", "RequestVersionExact", "--concurrency", cell.Connections.ToString(CultureInfo.InvariantCulture), "--duration", $"{cell.DurationSeconds.ToString(CultureInfo.InvariantCulture)}s", "--warmup", $"{cell.WarmupSeconds.ToString(CultureInfo.InvariantCulture)}s", targetUrl.ToString()],
            _ => [.. manifest.DefaultArguments, targetUrl.ToString()]
        };
    }

    internal static IReadOnlyList<string> BuildArguments(string toolName, Uri targetUrl, RunCell cell)
    {
        return BuildArguments(new LoadToolManifest { Id = toolName }, targetUrl, cell);
    }

    internal static LoadShapeSemantics BuildLoadShapeSemantics(LoadToolManifest manifest, RunCell cell)
    {
        var protocol = cell.Protocol.ToLowerInvariant();
        var warnings = new List<string>();
        var supported = new List<string> { "connections", "concurrency", "durationSeconds", "warmupSeconds", "repetitions" };
        var ignored = new List<string>();
        var derived = new List<string>();

        if (protocol == "h1")
        {
            ignored.Add("streamsPerConnection");
            if (cell.StreamsPerConnection != 1)
            {
                warnings.Add("HTTP/1.1 does not support streamsPerConnection; the requested value is recorded but ignored by the effective load shape.");
            }
        }
        else if (string.Equals(manifest.Id, "h2load", StringComparison.OrdinalIgnoreCase))
        {
            supported.Add("streamsPerConnection");
            derived.Add("concurrency = connections * streamsPerConnection");
        }
        else if (string.Equals(manifest.Id, ManagedHttp3LoadGenerator.ToolId, StringComparison.OrdinalIgnoreCase))
        {
            ignored.Add("streamsPerConnection");
            derived.Add("concurrency = connections");
            if (cell.StreamsPerConnection != 1)
            {
                warnings.Add("managed-httpclient-h3-load uses concurrency as the primary control and does not guarantee exact streamsPerConnection.");
            }

            warnings.Add("managed-httpclient-h3-load does not guarantee exact connection count or exact stream-per-connection mapping; treat results as local managed-lab measurements.");
        }
        else if (string.Equals(manifest.Id, "oha", StringComparison.OrdinalIgnoreCase))
        {
            ignored.Add("streamsPerConnection");
            if (cell.StreamsPerConnection != 1)
            {
                warnings.Add($"oha is used as a concurrency-style load generator here; streamsPerConnection={cell.StreamsPerConnection} is not mapped to an oha parallel stream setting.");
            }
        }

        return new LoadShapeSemantics
        {
            Protocol = cell.Protocol,
            LoadTool = manifest.Id,
            SupportedFields = supported,
            IgnoredFields = ignored,
            DerivedFields = derived,
            UnsupportedFields = [],
            Warnings = warnings
        };
    }

    internal static LoadToolParseResult Parse(LoadToolManifest manifest, string stdout, string stderr)
    {
        var parserId = manifest.GetEffectiveParserType().ToLowerInvariant();
        return parserId switch
        {
            "h2load" => H2loadParser.Parse(stdout + Environment.NewLine + stderr),
            "oha-json" => OhaJsonParser.Parse(stdout),
            "managed-httpclient-h3-json" => ManagedHttp3JsonParser.Parse(stdout),
            _ => new LoadToolParseResult(false, new HttpMetrics(), [$"No parser is implemented for load tool '{manifest.Id}' (parserId={parserId})."])
        };
    }

    internal static LoadToolParseResult Parse(string toolName, string stdout, string stderr)
    {
        return Parse(new LoadToolManifest { Id = toolName, OutputParserId = toolName }, stdout, stderr);
    }

    private static LoadToolResolution ResolveManifest(LoadToolManifest manifest, string? requestedMode)
    {
        var mode = string.IsNullOrWhiteSpace(requestedMode) ? manifest.Kind : requestedMode;
        if (string.Equals(mode, LoadToolKinds.Managed, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(manifest.Id, ManagedHttp3LoadGenerator.ToolId, StringComparison.OrdinalIgnoreCase))
            {
                return new LoadToolResolution(
                    null,
                    new LoadToolExecutionResult
                    {
                        Status = LoadToolExecutionStatuses.Unsupported,
                        ToolId = manifest.Id,
                        ToolName = manifest.Name,
                        Mode = mode,
                        Category = manifest.Category,
                        Errors = [$"Managed load-tool mode for '{manifest.Id}' is unsupported."]
                    });
            }

            var resolved = new ResolvedLoadTool(manifest, LoadToolKinds.Managed, null, null, RuntimeInformation.FrameworkDescription, []);
            return new LoadToolResolution(
                resolved,
                new LoadToolExecutionResult
                {
                    Status = LoadToolExecutionStatuses.Succeeded,
                    ToolId = manifest.Id,
                    ToolName = manifest.Name,
                    Mode = LoadToolKinds.Managed,
                    Category = manifest.Category,
                    Version = RuntimeInformation.FrameworkDescription
                });
        }

        if (string.Equals(mode, LoadToolKinds.Process, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, TargetKinds.Process, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(manifest.Executable))
            {
                return Unavailable(manifest.Id, mode, $"Load tool '{manifest.Id}' does not define a process executable.");
            }

            var executable = ResolveExecutable(manifest.Executable);
            if (executable is null)
            {
                return Unavailable(manifest.Id, mode, $"Load tool '{manifest.Id}' executable '{manifest.Executable}' was not found on PATH.");
            }

            var resolved = new ResolvedLoadTool(manifest, LoadToolKinds.Process, executable, null, null, []);
            return new LoadToolResolution(
                resolved,
                new LoadToolExecutionResult
                {
                    Status = LoadToolExecutionStatuses.Succeeded,
                    ToolId = manifest.Id,
                    ToolName = manifest.Name,
                    Mode = LoadToolKinds.Process,
                    Category = manifest.Category,
                    ExecutablePath = executable
                });
        }

        if (string.Equals(mode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            var docker = ResolveExecutable("docker");
            if (docker is null)
            {
                return new LoadToolResolution(
                    null,
                    new LoadToolExecutionResult
                    {
                        Status = LoadToolExecutionStatuses.Unavailable,
                        ToolId = manifest.Id,
                        ToolName = manifest.Name,
                        Mode = LoadToolKinds.Docker,
                        Category = manifest.Category,
                        DockerImage = manifest.DockerImage,
                        Errors = ["Docker executable was not found on PATH."]
                    });
            }

            if (string.IsNullOrWhiteSpace(manifest.DockerImage))
            {
                return new LoadToolResolution(
                    null,
                    new LoadToolExecutionResult
                    {
                        Status = LoadToolExecutionStatuses.Unavailable,
                        ToolId = manifest.Id,
                        ToolName = manifest.Name,
                        Mode = LoadToolKinds.Docker,
                        Category = manifest.Category,
                        Errors = [$"Load tool '{manifest.Id}' does not define a Docker image."]
                    });
            }

            var resolved = new ResolvedLoadTool(manifest, LoadToolKinds.Docker, docker, manifest.DockerImage, null, []);
            return new LoadToolResolution(
                resolved,
                new LoadToolExecutionResult
                {
                    Status = LoadToolExecutionStatuses.Succeeded,
                    ToolId = manifest.Id,
                    ToolName = manifest.Name,
                    Mode = LoadToolKinds.Docker,
                    Category = manifest.Category,
                    ExecutablePath = docker,
                    DockerImage = manifest.DockerImage,
                });
        }

        return new LoadToolResolution(
            null,
            new LoadToolExecutionResult
            {
                Status = LoadToolExecutionStatuses.Unsupported,
                ToolId = manifest.Id,
                ToolName = manifest.Name,
                Mode = mode,
                Category = manifest.Category,
                Errors = [$"Load tool mode '{mode}' is unsupported."]
            });
    }

    private static EffectiveLoadShape BuildEffectiveLoadShape(LoadToolManifest manifest, RunCell cell, LoadShapeSemantics semantics)
    {
        var protocol = cell.Protocol.ToLowerInvariant();
        var streams = protocol == "h1" ||
            string.Equals(manifest.Id, "oha", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(manifest.Id, ManagedHttp3LoadGenerator.ToolId, StringComparison.OrdinalIgnoreCase)
            ? 1
            : cell.StreamsPerConnection;
        var concurrency = string.Equals(manifest.Id, "h2load", StringComparison.OrdinalIgnoreCase) && protocol is "h2" or "h3"
            ? cell.Connections * cell.StreamsPerConnection
            : cell.Connections;

        return new EffectiveLoadShape
        {
            Connections = cell.Connections,
            Concurrency = concurrency,
            StreamsPerConnection = streams,
            DurationSeconds = cell.DurationSeconds,
            WarmupSeconds = cell.WarmupSeconds,
            Repetitions = cell.Repetition,
            Notes =
            [
                string.Equals(manifest.Id, ManagedHttp3LoadGenerator.ToolId, StringComparison.OrdinalIgnoreCase)
                    ? "managed-httpclient-h3-load maps the run to managed HttpClient concurrency and duration; exact connection count and streamsPerConnection are not guaranteed."
                    : string.Equals(manifest.Id, "oha", StringComparison.OrdinalIgnoreCase)
                    ? "oha maps the run to concurrency and duration; streamsPerConnection is not a separate HTTP/1.1 concept."
                    : "h2load maps connections to clients and streamsPerConnection to max concurrent streams where the negotiated protocol supports streams."
            ],
            Warnings = semantics.Warnings
        };
    }

    private static string FormatCommandLine(string executable, IReadOnlyList<string> arguments)
    {
        return string.Join(" ", new[] { QuoteArgument(executable) }.Concat(arguments.Select(QuoteArgument)));
    }

    private static string QuoteArgument(string argument)
    {
        return argument.Contains(' ', StringComparison.Ordinal) || argument.Contains('"', StringComparison.Ordinal)
            ? "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : argument;
    }

    private static IReadOnlyList<string> BuildOhaProtocolArguments(string protocol)
    {
        return protocol.ToLowerInvariant() switch
        {
            "h1" => ["--http-version", "1.1"],
            "h2" => ["--http-version", "2"],
            "h3" => ["--http-version", "3"],
            _ => []
        };
    }

    private static IReadOnlyList<string> BuildH2loadProtocolArguments(
        LoadToolManifest manifest,
        string protocol,
        ArtifactPaths? paths,
        string? mode,
        string? connectTarget,
        bool captureQlog)
    {
        if (!protocol.Equals("h3", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var arguments = new List<string> { "--h3" };
        if (!string.IsNullOrWhiteSpace(manifest.Sni))
        {
            arguments.Add("--sni");
            arguments.Add(manifest.Sni);
        }

        if (!string.IsNullOrWhiteSpace(connectTarget))
        {
            arguments.Add($"--connect-to={connectTarget}");
        }

        if (paths is not null && captureQlog)
        {
            var qlogBase = string.Equals(mode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase)
                    ? "/artifacts/qlog/h2load"
                    : Path.Combine(paths.QlogDirectory, "h2load");
            arguments.Add("--qlog-file-base");
            arguments.Add(qlogBase);
        }

        return arguments;
    }

    private static IReadOnlyList<string> BuildH2loadOutputArguments(RunCell cell, ArtifactPaths? paths, string? mode)
    {
        if (paths is null)
        {
            return [];
        }

        var outputPath = string.Equals(mode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase)
                ? "/artifacts/h2load-output.json"
                : paths.H2loadOutputJson;

        return ["--output-file", outputPath];
    }

    private static LoadToolTargetRoute ResolveTargetRoute(ResolvedLoadTool tool, Uri targetUrl, TargetExecutionResult? targetExecution)
    {
        if (!IsDockerTool(tool))
        {
            return new LoadToolTargetRoute(targetUrl, null, null, null);
        }

        if (targetExecution is not null &&
            string.Equals(targetExecution.TargetDockerNetworkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(targetExecution.TargetDockerNetworkName))
        {
            var internalPort = ResolveInternalPort(targetExecution, targetUrl.Port);
            var builder = new UriBuilder(targetUrl)
            {
                Host = string.IsNullOrWhiteSpace(tool.Manifest.Sni) ? "localhost" : tool.Manifest.Sni,
                Port = internalPort
            };
            var alias = targetExecution.TargetNetworkAliases.FirstOrDefault() ??
                targetExecution.TargetContainerName ??
                targetUrl.Host;
            return new LoadToolTargetRoute(
                builder.Uri,
                $"{alias}:{internalPort.ToString(CultureInfo.InvariantCulture)}",
                "shared-docker-network-connect-to",
                targetExecution.TargetDockerNetworkName);
        }

        if (!string.Equals(tool.Manifest.DockerHostRewrite, "host.docker.internal", StringComparison.OrdinalIgnoreCase) ||
            !IsLoopback(targetUrl))
        {
            return new LoadToolTargetRoute(targetUrl, GetConnectTarget(tool, targetUrl), null, null);
        }

        var rewrite = new UriBuilder(targetUrl)
        {
            Host = "host.docker.internal"
        }.Uri;
        return new LoadToolTargetRoute(
            rewrite,
            GetConnectTarget(tool, rewrite),
            $"{targetUrl.Host}->{rewrite.Host}",
            null);
    }

    private static int ResolveInternalPort(TargetExecutionResult targetExecution, int fallback)
    {
        var udp = targetExecution.TargetInternalPorts.FirstOrDefault(pair =>
            pair.Key.Contains("h3", StringComparison.OrdinalIgnoreCase) &&
            pair.Value.EndsWith("/udp", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(udp.Value))
        {
            udp = targetExecution.TargetInternalPorts.FirstOrDefault(pair =>
                pair.Value.EndsWith("/udp", StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(udp.Value))
        {
            var portText = udp.Value.Split('/', 2)[0];
            if (int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
            {
                return port;
            }
        }

        return fallback;
    }

    private static string? GetHostRewriteMode(ResolvedLoadTool tool, Uri requestedUrl, Uri effectiveUrl)
    {
        if (!IsDockerTool(tool) || string.Equals(requestedUrl.Host, effectiveUrl.Host, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"{requestedUrl.Host}->{effectiveUrl.Host}";
    }

    private static string? GetConnectTarget(ResolvedLoadTool tool, Uri effectiveUrl)
    {
        return IsDockerTool(tool)
            ? $"{effectiveUrl.Host}:{effectiveUrl.Port}"
            : null;
    }

    private static string? GetSni(ResolvedLoadTool tool, Uri requestedUrl)
    {
        if (!IsDockerTool(tool))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(tool.Manifest.Sni)
            ? requestedUrl.Host
            : tool.Manifest.Sni;
    }

    private static string? BuildDockerCommandLine(
        ResolvedLoadTool tool,
        IReadOnlyList<string> arguments,
        ArtifactPaths paths,
        string? dockerNetwork,
        DockerResourceLimits? resourceLimits,
        string? containerName)
    {
        if (!IsDockerTool(tool))
        {
            return null;
        }

        return FormatCommandLine(tool.ExecutablePath ?? "docker", BuildDockerRunArguments(tool, arguments, paths, dockerNetwork, resourceLimits, containerName));
    }

    private static async Task<string?> CaptureDockerVersionAsync(ResolvedLoadTool tool, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(tool.ExecutablePath))
        {
            return null;
        }

        try
        {
            if ((await RunProcessAsync(tool.ExecutablePath, ["image", "inspect", tool.DockerImage ?? tool.Manifest.DockerImage])).ExitCode != 0)
            {
                return null;
            }

            var run = await RunProcessAsync(tool.ExecutablePath, BuildDockerRunArguments(tool, arguments, paths: null, dockerNetwork: null));
            var combined = string.Join(Environment.NewLine, new[] { run.Stdout, run.Stderr }
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(combined)
                ? null
                : combined.Trim();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return $"docker version unavailable: {ex.Message}";
        }
    }

    internal static async Task<ToolCapability> DetectH2loadDockerH3CapabilityAsync(LoadToolManifest manifest, bool pullIfMissing)
    {
        var docker = ResolveExecutable("docker");
        if (docker is null)
        {
            return new ToolCapability(false, "docker-unavailable", Warnings: ["Docker executable was not found on PATH."]);
        }

        if (string.IsNullOrWhiteSpace(manifest.DockerImage))
        {
            return new ToolCapability(false, "docker-image-not-configured", docker);
        }

        var warnings = new List<string>();
        var imagePresent = (await RunProcessAsync(docker, ["image", "inspect", manifest.DockerImage])).ExitCode == 0;
        if (!imagePresent)
        {
            if (pullIfMissing && manifest.DockerAutoPull)
            {
                var pull = await RunProcessAsync(docker, ["pull", manifest.DockerImage]);
                if (pull.ExitCode != 0)
                {
                    var stderr = string.IsNullOrWhiteSpace(pull.Stderr) ? pull.Stdout : pull.Stderr;
                    return new ToolCapability(false, "docker-image-pull-failed", docker, Warnings: [$"Failed to pull {manifest.DockerImage}: {FirstLine(stderr) ?? "no output"}"]);
                }
            }
            else
            {
                var manifestProbe = await RunProcessAsync(docker, ["manifest", "inspect", manifest.DockerImage]);
                if (manifestProbe.ExitCode != 0)
                {
                    warnings.Add($"Docker image '{manifest.DockerImage}' is not present locally and manifest inspection failed.");
                    return new ToolCapability(false, "docker-image-unavailable", docker, Warnings: warnings);
                }

                warnings.Add($"Docker image '{manifest.DockerImage}' is pullable but not present locally.");
                return new ToolCapability(true, "docker-image-pullable-not-local", docker, Warnings: warnings);
            }
        }

        var tool = new ResolvedLoadTool(manifest, LoadToolKinds.Docker, docker, manifest.DockerImage, null, []);
        var help = await RunProcessAsync(docker, BuildDockerRunArguments(tool, ["--help"], paths: null));
        var version = await RunProcessAsync(docker, BuildDockerRunArguments(tool, manifest.VersionCommand.Count == 0 ? ["--version"] : manifest.VersionCommand, paths: null));
        var combined = help.Stdout + Environment.NewLine + help.Stderr + Environment.NewLine + version.Stdout + Environment.NewLine + version.Stderr;
        var versionText = string.Join(Environment.NewLine, new[] { version.Stdout, version.Stderr }
            .Where(static value => !string.IsNullOrWhiteSpace(value))).Trim();
        var hasH3 = combined.Contains("--h3", StringComparison.OrdinalIgnoreCase);
        var hasOutputFile = combined.Contains("--output-file", StringComparison.OrdinalIgnoreCase);
        var hasQlog = combined.Contains("--qlog-file-base", StringComparison.OrdinalIgnoreCase);
        var hasConnectTo = combined.Contains("--connect-to", StringComparison.OrdinalIgnoreCase);
        var hasSni = combined.Contains("--sni", StringComparison.OrdinalIgnoreCase);

        if (!hasOutputFile)
        {
            warnings.Add("h2load help does not advertise --output-file; JSON parsing may be unavailable.");
        }

        if (!hasQlog)
        {
            warnings.Add("h2load help does not advertise --qlog-file-base; qlog output is not enabled.");
        }

        if (!hasConnectTo)
        {
            warnings.Add("h2load help does not advertise --connect-to; explicit host connection override is unavailable.");
        }

        if (!hasSni)
        {
            warnings.Add("h2load help does not advertise --sni; explicit SNI override is unavailable.");
        }

        return hasH3
            ? new ToolCapability(true, "h3-supported", docker, versionText, warnings)
            : new ToolCapability(false, "h3-unsupported", docker, versionText, [.. warnings, "h2load help does not advertise --h3."]);
    }

    private static async Task<LoadToolRun> RunDockerAsync(ResolvedLoadTool tool, LoadToolExecutionPlan plan)
    {
        if (string.IsNullOrWhiteSpace(tool.ExecutablePath))
        {
            return new LoadToolRun(1, "", "Docker executable path was not resolved.");
        }

        if (string.IsNullOrWhiteSpace(plan.DockerContainerName))
        {
            var dockerArguments = BuildDockerRunArguments(tool, plan.Arguments, plan.Paths, plan.DockerNetwork, plan.DockerResourceLimits, containerName: null);
            var run = await RunProcessAsync(tool.ExecutablePath, dockerArguments);
            return run with
            {
                DockerResourceLimitsRequested = plan.DockerResourceLimits,
                ResourceLimitWarnings = DockerResourceControl.BuildWarnings(plan.DockerResourceLimits, effective: null, targetContainer: false)
            };
        }

        var cleanupWarnings = new List<string>();
        await RunProcessAsync(tool.ExecutablePath, ["rm", "--force", plan.DockerContainerName]);
        var namedArguments = BuildDockerRunArguments(tool, plan.Arguments, plan.Paths, plan.DockerNetwork, plan.DockerResourceLimits, plan.DockerContainerName);
        var result = await RunNamedDockerLoadToolAsync(tool.ExecutablePath, namedArguments, plan);
        await CaptureDockerInspectAsync(tool.ExecutablePath, plan.DockerContainerName, plan.Paths.LoadToolDockerInspectJson);
        var resourceWarnings = new List<string>();
        var effective = DockerResourceControl.ParseEffectiveLimitsFromInspectFile(plan.Paths.LoadToolDockerInspectJson, resourceWarnings);
        resourceWarnings.AddRange(DockerResourceControl.BuildWarnings(plan.DockerResourceLimits, effective, targetContainer: false));
        if (result.DockerMetricsSummary is not null)
        {
            var saturation = DockerContainerMetricsCapture.AssessSaturation(result.DockerMetricsSummary, plan.DockerResourceLimits, effective);
            result = result with
            {
                SaturationStatus = saturation.Status,
                SaturationWarnings = result.SaturationWarnings
                    .Concat(saturation.Warnings)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };
        }

        var remove = await RunProcessAsync(tool.ExecutablePath, ["rm", "--force", plan.DockerContainerName]);
        var cleanup = new DockerCleanupSummary
        {
            LoadToolContainerCleanupAttempted = true,
            LoadToolContainerCleanupSucceeded = remove.ExitCode == 0,
            LoadToolContainerName = plan.DockerContainerName,
            LoadToolMetricsSamplerCleanupAttempted = plan.CaptureDockerMetrics,
            LoadToolMetricsSamplerCleanupSucceeded = plan.CaptureDockerMetrics ? result.DockerMetricsSummary is not null : null,
            Errors = remove.ExitCode == 0 ? [] : [$"docker rm exited with code {remove.ExitCode}: {remove.Stderr.Trim()}"],
            Warnings = cleanupWarnings.Concat(result.SaturationWarnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
        await File.WriteAllTextAsync(plan.Paths.DockerCleanupJson, ResultJson.Serialize(cleanup));
        return result with
        {
            ContainerName = plan.DockerContainerName,
            DockerInspectPath = plan.Paths.LoadToolDockerInspectJson,
            DockerResourceLimitsRequested = plan.DockerResourceLimits,
            DockerResourceLimitsEffective = effective,
            ResourceLimitWarnings = resourceWarnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            CleanupSummary = cleanup
        };
    }

    private static async Task<LoadToolRun> RunNamedDockerLoadToolAsync(
        string docker,
        IReadOnlyList<string> arguments,
        LoadToolExecutionPlan plan)
    {
        if (!plan.CaptureDockerMetrics)
        {
            return await RunProcessAsync(docker, arguments);
        }

        var capture = await DockerContainerMetricsCapture.CaptureWhileRunningAsync(
            docker,
            plan.DockerContainerName!,
            plan.Paths,
            plan.DockerMetricsInterval,
            () => RunProcessAsync(docker, arguments));
        var saturation = DockerContainerMetricsCapture.AssessSaturation(
            capture.Summary,
            plan.DockerResourceLimits,
            effective: null);
        var warnings = capture.Warnings
            .Concat(saturation.Warnings)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return capture.Run with
        {
            ContainerId = capture.ContainerId,
            DockerMetricsAvailable = capture.Summary?.Samples.Count > 0,
            DockerMetricsSummary = capture.Summary,
            SaturationStatus = saturation.Status,
            SaturationWarnings = warnings,
            DockerMetricsArtifacts = capture.Artifacts
        };
    }

    private static IReadOnlyList<string> BuildDockerRunArguments(
        ResolvedLoadTool tool,
        IReadOnlyList<string> toolArguments,
        ArtifactPaths? paths,
        string? dockerNetwork = null,
        DockerResourceLimits? resourceLimits = null,
        string? containerName = null)
    {
        var args = new List<string> { "run" };
        if (string.IsNullOrWhiteSpace(containerName))
        {
            args.Add("--rm");
        }
        else
        {
            args.Add("--name");
            args.Add(containerName);
        }

        args.Add("--label");
        args.Add("incursa.protocol-lab.load-tool=true");
        args.Add("--label");
        args.Add($"incursa.protocol-lab.load-tool-id={tool.Manifest.Id}");

        if (!string.IsNullOrWhiteSpace(dockerNetwork))
        {
            args.Add("--network");
            args.Add(dockerNetwork);
        }

        DockerResourceControl.AddDockerRunArguments(args, resourceLimits);

        if (paths is not null)
        {
            args.Add("-v");
            args.Add($"{Path.GetFullPath(paths.CellDirectory)}:/artifacts");
        }

        foreach (var pair in tool.Manifest.DockerEnvironment)
        {
            args.Add("-e");
            args.Add($"{pair.Key}={pair.Value}");
        }

        args.AddRange(tool.Manifest.DockerArguments);
        args.Add(tool.DockerImage ?? tool.Manifest.DockerImage);

        if (!string.IsNullOrWhiteSpace(tool.Manifest.DockerCommand))
        {
            args.Add(tool.Manifest.DockerCommand);
        }

        args.AddRange(toolArguments);
        return args;
    }

    private static async Task CaptureDockerInspectAsync(string docker, string containerName, string path)
    {
        var inspect = await RunProcessAsync(docker, ["inspect", containerName]);
        await File.WriteAllTextAsync(path, inspect.ExitCode == 0 ? inspect.Stdout : inspect.Stderr);
    }

    private static string BuildLoadToolContainerName(RunCell cell, ArtifactPaths paths)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(paths.CellDirectory.ToUpperInvariant()));
        var hash = Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
        var raw = $"protocol-lab-loadtool-{cell.Implementation.Id}-{cell.Scenario.Id}-{cell.Repetition}-{hash}";
        var sanitized = new string(raw
            .Select(static character => char.IsAsciiLetterOrDigit(character) || character == '-' ? char.ToLowerInvariant(character) : '-')
            .ToArray())
            .Trim('-');
        return sanitized[..Math.Min(63, sanitized.Length)];
    }

    private static bool IsDockerTool(ResolvedLoadTool tool)
    {
        return string.Equals(tool.Mode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tool.Mode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoopback(Uri uri)
    {
        return IPAddress.TryParse(uri.Host, out var address)
            ? IPAddress.IsLoopback(address)
            : string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstLine(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
    }

    private static LoadToolResolution Unavailable(string? toolId, string? mode, string reason)
    {
        return new LoadToolResolution(
            null,
            new LoadToolExecutionResult
            {
                Status = LoadToolExecutionStatuses.Unavailable,
                ToolId = toolId,
                Mode = mode,
                Errors = [reason]
            });
    }

    private static bool IsCompatible(LoadToolManifest manifest, RunCell cell)
    {
        if (!manifest.SupportsProtocol(cell.Protocol))
        {
            return false;
        }

        if (!manifest.SupportedScenarioFamilies.Contains(cell.Scenario.Family, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var trafficShape = cell.Scenario.TrafficShape;
        if (!string.IsNullOrWhiteSpace(trafficShape) &&
            manifest.GetEffectiveTrafficShapes().Count > 0 &&
            !manifest.SupportsTrafficShape(trafficShape))
        {
            return false;
        }

        return true;
    }

    private static int GetSelectionPriority(LoadToolManifest manifest, RunCell cell)
    {
        if (string.Equals(cell.Protocol, "h3", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(manifest.Id, "h2load", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(manifest.Id, ManagedHttp3LoadGenerator.ToolId, StringComparison.OrdinalIgnoreCase))
            {
                return 10;
            }

            if (string.Equals(manifest.Id, "oha", StringComparison.OrdinalIgnoreCase))
            {
                return 100;
            }
        }

        return 50;
    }

    private static async Task<LoadToolRun> RunProcessAsync(string executable, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.Environment["NO_COLOR"] = "true";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start load tool '{executable}'.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new LoadToolRun(process.ExitCode, await stdout, await stderr);
    }

    private static IReadOnlyList<string> GetExecutableExtensions()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [""];
        }

        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrWhiteSpace(pathExt))
        {
            return [".exe", ".cmd", ".bat", ""];
        }

        return pathExt
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Append("")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

internal static partial class H2loadParser
{
    public static LoadToolParseResult Parse(string output)
    {
        if (LooksLikeJson(output))
        {
            var json = TryParseJson(output);
            if (json is not null)
            {
                return json;
            }
        }

        var metrics = new HttpMetrics
        {
            RequestsPerSecond = ParseDouble(RegexMatch(output, @"finished in .+?,\s*([\d.]+)\s*req/s")),
            TotalRequests = ParseLong(RegexMatch(output, @"requests:\s*(\d+)\s+total")),
            ThroughputBytesPerSecond = ParseThroughput(output),
            SuccessfulRequests = ParseLong(RegexMatch(output, @"requests:\s*\d+\s+total,\s*\d+\s+started,\s*\d+\s+done,\s*(\d+)\s+succeeded")),
            FailedRequests = ParseLong(RegexMatch(output, @"requests:.*?,\s*(\d+)\s+failed")),
            TimeoutRequests = ParseLong(RegexMatch(output, @"requests:.*?,\s*(\d+)\s+timeout")),
            LatencyMinMs = ParseDurationMilliseconds(DurationMatch(output, @"time for request:\s*min\s*([\d.]+)\s*([a-z]+),")),
            LatencyMaxMs = ParseDurationMilliseconds(DurationMatch(output, @"time for request:.*?max\s*([\d.]+)\s*([a-z]+),")),
            LatencyMeanMs = ParseDurationMilliseconds(DurationMatch(output, @"time for request:.*?mean\s*([\d.]+)\s*([a-z]+),")),
            LatencyP50Ms = ParsePercentileMilliseconds(output, "50%"),
            LatencyP75Ms = ParsePercentileMilliseconds(output, "75%"),
            LatencyP90Ms = ParsePercentileMilliseconds(output, "90%"),
            LatencyP95Ms = ParsePercentileMilliseconds(output, "95%"),
            LatencyP99Ms = ParsePercentileMilliseconds(output, "99%")
        };

        var parsed = metrics.RequestsPerSecond is not null ||
            metrics.SuccessfulRequests is not null ||
            metrics.FailedRequests is not null ||
            metrics.LatencyMeanMs is not null ||
            metrics.ThroughputBytesPerSecond is not null;
        var warnings = parsed
            ? Array.Empty<string>()
            : ["h2load output was preserved, but no metrics were parsed."];

        return new LoadToolParseResult(parsed, metrics, warnings);
    }

    private static bool LooksLikeJson(string output)
    {
        return output.TrimStart().StartsWith("{", StringComparison.Ordinal);
    }

    private static LoadToolParseResult? TryParseJson(string output)
    {
        try
        {
            var jsonText = ExtractFirstJsonObject(output.TrimStart());
            using var document = JsonDocument.Parse(jsonText);
            var root = document.RootElement;
            var metrics = new HttpMetrics
            {
                RequestsPerSecond = FindNumber(root, "requestsPerSecond", "requestsPerSec", "request_per_second", "req/s", "rps"),
                TotalRequests = FindLong(root, "totalRequests", "total", "requests"),
                SuccessfulRequests = FindLong(root, "successfulRequests", "succeeded", "success", "done"),
                FailedRequests = FindLong(root, "failedRequests", "failed"),
                TimeoutRequests = FindLong(root, "timeoutRequests", "timeout", "timeouts"),
                BytesReceived = FindLong(root, "bytesReceived", "totalBytes", "bytes"),
                ThroughputBytesPerSecond = FindNumber(root, "throughputBytesPerSecond", "bytesPerSecond", "bytes_per_second", "sizePerSec"),
                LatencyMinMs = FindDurationMilliseconds(root, "latencyMinMs", "min"),
                LatencyMeanMs = FindDurationMilliseconds(root, "latencyMeanMs", "mean", "average"),
                LatencyP50Ms = FindDurationMilliseconds(root, "latencyP50Ms", "p50", "median", "50"),
                LatencyP95Ms = FindDurationMilliseconds(root, "latencyP95Ms", "p95", "95"),
                LatencyP99Ms = FindDurationMilliseconds(root, "latencyP99Ms", "p99", "99"),
                LatencyMaxMs = FindDurationMilliseconds(root, "latencyMaxMs", "max"),
                StatusCodeCounts = FindStatusCodeCounts(root)
            };

            var parsed = metrics.RequestsPerSecond is not null ||
                metrics.TotalRequests is not null ||
                metrics.SuccessfulRequests is not null ||
                metrics.LatencyMeanMs is not null;
            return new LoadToolParseResult(
                parsed,
                metrics,
                parsed ? [] : ["h2load JSON output was preserved, but no metrics were parsed."]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ExtractFirstJsonObject(string output)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < output.Length; i++)
        {
            var current = output[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return output[..(i + 1)];
                }
            }
        }

        return output;
    }

    private static long? FindLong(JsonElement element, params string[] names)
    {
        var number = FindNumber(element, names);
        return number.HasValue ? Convert.ToInt64(number.Value, CultureInfo.InvariantCulture) : null;
    }

    private static double? FindDurationMilliseconds(JsonElement element, params string[] names)
    {
        var value = FindNumber(element, names);
        if (value is null)
        {
            return null;
        }

        return value.Value > 0 && value.Value < 1 ? value.Value * 1000d : value.Value;
    }

    private static double? FindNumber(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) &&
                    TryGetNumber(property.Value, out var direct))
                {
                    return direct;
                }

                var nested = FindNumber(property.Value, names);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindNumber(item, names);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static bool TryGetNumber(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDouble(out value);
        }

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static Dictionary<string, long> FindStatusCodeCounts(JsonElement element)
    {
        var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        FindStatusCodeCounts(element, counts);
        return counts;
    }

    private static void FindStatusCodeCounts(JsonElement element, Dictionary<string, long> counts)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if ((property.Name.Contains("status", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Contains("code", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var status in property.Value.EnumerateObject())
                    {
                        if (status.Value.ValueKind == JsonValueKind.Number &&
                            status.Value.TryGetInt64(out var count) &&
                            (int.TryParse(status.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                             status.Name.EndsWith("xx", StringComparison.OrdinalIgnoreCase)))
                        {
                            counts[status.Name] = count;
                        }
                    }
                }

                FindStatusCodeCounts(property.Value, counts);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                FindStatusCodeCounts(item, counts);
            }
        }
    }

    private static string? RegexMatch(string output, string pattern)
    {
        var match = Regex.Match(output, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? DurationMatch(string output, string pattern)
    {
        var match = Regex.Match(output, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value + match.Groups[2].Value : null;
    }

    private static double? ParseDouble(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? ParseLong(string? value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double? ParseThroughput(string output)
    {
        var match = Regex.Match(output, @"finished in .+?,\s*[\d.]+\s*req/s,\s*([\d.]+)\s*([KMGT]?B)/s", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        var multiplier = match.Groups[2].Value.ToUpperInvariant() switch
        {
            "KB" => 1024d,
            "MB" => 1024d * 1024d,
            "GB" => 1024d * 1024d * 1024d,
            "TB" => 1024d * 1024d * 1024d * 1024d,
            _ => 1d
        };
        return number * multiplier;
    }

    private static double? ParseDurationMilliseconds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(value, @"^([\d.]+)\s*([a-z]+)$", RegexOptions.IgnoreCase);
        if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        return match.Groups[2].Value.ToLowerInvariant() switch
        {
            "us" => number / 1000d,
            "ms" => number,
            "s" => number * 1000d,
            _ => null
        };
    }

    private static double? ParsePercentileMilliseconds(string output, string percentile)
    {
        var escaped = Regex.Escape(percentile);
        var match = Regex.Match(output, $@"{escaped}\s+([\d.]+)([a-z]+)", RegexOptions.IgnoreCase);
        return match.Success ? ParseDurationMilliseconds(match.Groups[1].Value + match.Groups[2].Value) : null;
    }
}

internal static class OhaJsonParser
{
    public static LoadToolParseResult Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new LoadToolParseResult(false, new HttpMetrics(), ["oha output was empty."]);
        }

        try
        {
            using var document = JsonDocument.Parse(output);
            var totalRequests = SumObjectIntegerValues(document.RootElement, "responseTimeHistogram");
            var successRate = FindNumber(document.RootElement, "successRate");
            long? successfulRequests = totalRequests.HasValue && successRate.HasValue
                ? Convert.ToInt64(Math.Round(totalRequests.Value * successRate.Value), CultureInfo.InvariantCulture)
                : null;
            long? failedRequests = totalRequests.HasValue && successfulRequests.HasValue
                ? totalRequests.Value - successfulRequests.Value
                : null;
            var metrics = new HttpMetrics
            {
                RequestsPerSecond = FindNumber(document.RootElement, "requestsPerSec", "requestsPerSecond", "rps"),
                TotalRequests = totalRequests,
                SuccessfulRequests = successfulRequests,
                FailedRequests = failedRequests,
                ThroughputBytesPerSecond = FindNumber(document.RootElement, "sizePerSec", "throughputBytesPerSecond"),
                LatencyMeanMs = SecondsToMilliseconds(FindNumber(document.RootElement, "average", "mean")),
                LatencyP50Ms = SecondsToMilliseconds(FindNumber(document.RootElement, "p50", "50")),
                LatencyP95Ms = SecondsToMilliseconds(FindNumber(document.RootElement, "p95", "95")),
                LatencyP99Ms = SecondsToMilliseconds(FindNumber(document.RootElement, "p99", "99"))
            };

            var parsed = metrics.RequestsPerSecond is not null ||
                metrics.TotalRequests is not null ||
                metrics.LatencyMeanMs is not null;
            return new LoadToolParseResult(
                parsed,
                metrics,
                parsed ? [] : ["oha JSON output was preserved, but no metrics were parsed."]);
        }
        catch (JsonException ex)
        {
            return new LoadToolParseResult(false, new HttpMetrics(), [$"oha output was not valid JSON: {ex.Message}"]);
        }
    }

    private static double? SecondsToMilliseconds(double? value)
    {
        return value.HasValue ? value.Value * 1000d : null;
    }

    private static long? FindLong(JsonElement element, params string[] names)
    {
        var number = FindNumber(element, names);
        return number.HasValue ? Convert.ToInt64(number.Value, CultureInfo.InvariantCulture) : null;
    }

    private static long? SumObjectIntegerValues(JsonElement element, string objectName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, objectName, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.Object)
                {
                    long total = 0;
                    foreach (var bucket in property.Value.EnumerateObject())
                    {
                        if (bucket.Value.ValueKind == JsonValueKind.Number &&
                            bucket.Value.TryGetInt64(out var count))
                        {
                            total += count;
                        }
                    }

                    return total;
                }

                var nested = SumObjectIntegerValues(property.Value, objectName);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = SumObjectIntegerValues(item, objectName);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static double? FindNumber(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) &&
                    TryGetNumber(property.Value, out var direct))
                {
                    return direct;
                }

                var nested = FindNumber(property.Value, names);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindNumber(item, names);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static bool TryGetNumber(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDouble(out value);
        }

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }
}
