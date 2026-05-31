// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Incursa.ProtocolLab.Model;
using System.Runtime.InteropServices;

namespace Incursa.ProtocolLab.Runner;

public sealed class RunnerEngine
{
    private sealed record ProcessProbe(int ExitCode, string Stdout, string Stderr);

    public async Task<RunnerCommandResult> CheckAsync(string root, IRunnerEventSink? eventSink = null)
    {
        var output = new RunnerOutputBuffer(RunnerCommandKind.Check, eventSink);
        var warnings = new List<string>();
        output.WriteLine("ProtocolLab check");
        output.WriteLine($"root: {Path.GetFullPath(root)}");
        output.WriteLine();

        var dotnet = LoadToolInvoker.ResolveExecutable("dotnet");
        if (dotnet is null)
        {
            warnings.Add("Install the .NET SDK and ensure dotnet is on PATH.");
            output.WriteLine(".NET SDK: unavailable");
        }
        else
        {
            var sdkVersion = await RunProbeAsync(dotnet, ["--version"], root);
            output.WriteLine($".NET SDK: available path={dotnet} version={FirstLine(sdkVersion.Stdout) ?? FirstLine(sdkVersion.Stderr) ?? "unknown"}");
            output.WriteLine($".NET runtime: {RuntimeInformation.FrameworkDescription}");

            var toolManifestPath = Path.Combine(root, "dotnet-tools.json");
            var toolList = await RunProbeAsync(dotnet, ["tool", "list", "--local"], root);
            var toolManifestFound = File.Exists(toolManifestPath);
            var dotnetCountersListed = toolList.Stdout.Contains("dotnet-counters", StringComparison.OrdinalIgnoreCase);
            output.WriteLine($"dotnet tool manifest: {(toolManifestFound ? "found" : "missing")} path={toolManifestPath}");
            output.WriteLine($"dotnet tool restore state: {(dotnetCountersListed ? "dotnet-counters listed" : "dotnet-counters not listed")} remediation=dotnet tool restore");
            if (!toolManifestFound || !dotnetCountersListed)
            {
                warnings.Add("Run 'dotnet tool restore' from the repository root before counter-enabled acceptance.");
            }
        }

        output.WriteLine();
        var docker = LoadToolInvoker.ResolveExecutable("docker");
        if (docker is null)
        {
            warnings.Add("Install/start Docker Desktop before external-reference h2load acceptance, or use -SkipExternal.");
            output.WriteLine("Docker: unavailable remediation=Install/start Docker Desktop.");
        }
        else
        {
            var dockerVersion = await RunProbeAsync(docker, ["version", "--format", "{{.Server.Version}}"], root);
            var version = dockerVersion.ExitCode == 0
                ? FirstLine(dockerVersion.Stdout)
                : FirstLine(dockerVersion.Stderr) ?? "version unavailable";
            output.WriteLine($"Docker: available path={docker} serverVersion={version}");
            var info = await RunProbeAsync(docker, ["info", "--format", "OSType={{.OSType}} CgroupDriver={{.CgroupDriver}} CgroupVersion={{.CgroupVersion}} NCPU={{.NCPU}} MemTotal={{.MemTotal}}"], root);
            output.WriteLine($"Docker resource limits: supported=assumed-from-docker-run-flags backend={FirstLine(info.Stdout) ?? FirstLine(info.Stderr) ?? "unknown"}");
            output.WriteLine("  note: Docker Desktop limits are bounded by Docker Desktop settings; ProtocolLab records requested/effective container limits but keeps local evidence warnings.");
            if (dockerVersion.ExitCode != 0)
            {
                warnings.Add("Docker executable was found but Docker Engine did not answer 'docker version'. Start Docker Desktop.");
            }
        }

        output.WriteLine();
        var tools = LoadToolCatalog.Load(Path.Combine(root, "load-tools"));
        output.WriteLine("Load tools:");
        foreach (var tool in tools)
        {
            var managed = string.Equals(tool.Kind, LoadToolKinds.Managed, StringComparison.OrdinalIgnoreCase);
            var executable = string.IsNullOrWhiteSpace(tool.Executable) ? null : LoadToolInvoker.ResolveExecutable(tool.Executable);
            var available = managed || executable is not null;
            var version = available ? await LoadToolInvoker.CaptureVersionAsync(tool, executable ?? "") : null;
            output.WriteLine($"{tool.Id}\tmode={tool.Kind}\tcategory={tool.Category}\tavailable={(available ? "yes" : "no")}\tversion={FirstLine(version) ?? "n/a"}");
            output.WriteLine($"  protocols: {string.Join(",", tool.SupportedProtocols)}");
            output.WriteLine($"  families: {string.Join(",", tool.SupportedScenarioFamilies)}");
            if (!string.IsNullOrWhiteSpace(tool.Notes))
            {
                output.WriteLine($"  notes: {tool.Notes}");
            }
        }

        output.WriteLine();
        var curl = await ProtocolProofValidator.DetectCurlCapabilityAsync();
        output.WriteLine($"curl: available={(curl.Available ? "yes" : "no")} http3OnlySupport={curl.Status} version={FirstLine(curl.Version) ?? "n/a"}");
        foreach (var warning in curl.Warnings ?? [])
        {
            output.WriteLine($"  warning: {warning}");
        }
        if (!string.Equals(curl.Status, "http3-supported", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("curl does not provide usable --http3-only proof; ProtocolLab will use managed exact HTTP/3 validation when possible.");
        }

        var managedH3 = await ProtocolProofValidator.DetectManagedHttpClientH3CapabilityAsync();
        output.WriteLine($"managed H3 proof: available={(managedH3.Available ? "yes" : "no")} status={managedH3.Status} runtime={FirstLine(managedH3.Version) ?? "n/a"}");
        foreach (var warning in managedH3.Warnings ?? [])
        {
            output.WriteLine($"  note: {warning}");
        }

        var managedLoad = tools.FirstOrDefault(tool => string.Equals(tool.Id, ManagedHttp3LoadGenerator.ToolId, StringComparison.OrdinalIgnoreCase));
        output.WriteLine($"managed H3 load: available={(managedLoad is not null ? "yes" : "no")} manifest={(managedLoad is null ? "missing" : "found")}");
        if (managedLoad is null)
        {
            warnings.Add("load-tools/managed-httpclient-h3-load.yaml is required for managed-lab H3 acceptance.");
        }

        var h2load = await ProtocolProofValidator.DetectH2loadH3CapabilityAsync();
        output.WriteLine($"h2load process: available={(h2load.Available ? "yes" : "no")} h3Support={h2load.Status} version={FirstLine(h2load.Version) ?? "n/a"}");
        foreach (var warning in h2load.Warnings ?? [])
        {
            output.WriteLine($"  warning: {warning}");
        }
        await WriteH2loadProcessOptionProofAsync(root, output);

        var h2loadManifest = tools.FirstOrDefault(tool => string.Equals(tool.Id, "h2load", StringComparison.OrdinalIgnoreCase));
        if (h2loadManifest is not null)
        {
            var dockerH2load = await LoadToolInvoker.DetectH2loadDockerH3CapabilityAsync(h2loadManifest, pullIfMissing: false);
            output.WriteLine($"h2load Docker image: image={h2loadManifest.DockerImage} available={(dockerH2load.Available ? "yes" : "no")} h3Support={dockerH2load.Status} version={FirstLine(dockerH2load.Version) ?? "n/a"}");
            var dockerH2loadReady = string.Equals(dockerH2load.Status, "h3-supported", StringComparison.OrdinalIgnoreCase);
            output.WriteLine($"  --h3 proof: {FormatSupport(dockerH2loadReady)}");
            output.WriteLine($"  --output-file proof: {FormatSupport(dockerH2loadReady && !HasWarning(dockerH2load, "--output-file"))}");
            output.WriteLine($"  --qlog-file-base proof: {FormatSupport(dockerH2loadReady && !HasWarning(dockerH2load, "--qlog-file-base"))}");
            foreach (var warning in dockerH2load.Warnings ?? [])
            {
                output.WriteLine($"  warning: {warning}");
            }
            if (!string.Equals(dockerH2load.Status, "h3-supported", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add("Build the repo-owned h2load image with scripts\\build\\Build-H2LoadHttp3Image.ps1 before external-reference acceptance.");
            }
        }
        else
        {
            warnings.Add("load-tools/h2load.yaml is missing; external-reference h2load acceptance cannot run.");
        }

        var ohaH3 = await ProtocolProofValidator.DetectOhaH3CapabilityAsync();
        output.WriteLine($"oha: available={(ohaH3.Available ? "yes" : "no")} h3Signal={ohaH3.Status} version={FirstLine(ohaH3.Version) ?? "n/a"}");
        foreach (var warning in ohaH3.Warnings ?? [])
        {
            output.WriteLine($"  warning: {warning}");
        }

        var counters = await RuntimeCounterSession.DetectToolAsync("dotnet-counters", root);
        output.WriteLine($"dotnet-counters: available={(counters.Available ? "yes" : "no")} version={FirstLine(counters.Version) ?? "n/a"}");
        foreach (var warning in counters.Warnings)
        {
            output.WriteLine($"  warning: {warning}");
        }
        if (!counters.Available)
        {
            warnings.Add("Run 'dotnet tool restore' to enable counter-enabled acceptance.");
        }

        output.WriteLine();
        WriteManifestStatus(root, "Kestrel manifest", "implementations", "kestrel-http3.yaml", warnings, output);
        WriteManifestStatus(root, "Incursa manifest", "implementations", "incursa-http3.yaml", warnings, output);
        WriteManifestStatus(root, "Caddy manifest", "implementations", "caddy-http3.yaml", warnings, output);
        WriteManifestStatus(root, "nginx manifest", "implementations", "nginx-http3.yaml", warnings, output);
        await WriteDockerTargetImageStatusAsync(root, "Kestrel Docker target image", "implementations", "kestrel-http3.yaml", "scripts\\build\\Build-KestrelBenchServerImage.ps1", warnings, output);
        await WriteDockerTargetImageStatusAsync(root, "Incursa Docker target image", "implementations", "incursa-http3.yaml", "scripts\\build\\Build-IncursaHttp3BenchServerImage.ps1", warnings, output);
        await WriteDockerTargetImageStatusAsync(root, "Caddy Docker target image", "implementations", "caddy-http3.yaml", "scripts\\build\\Build-CaddyBenchServerImage.ps1", warnings, output);
        await WriteDockerTargetImageStatusAsync(root, "nginx Docker target image", "implementations", "nginx-http3.yaml", "scripts\\build\\Build-NginxBenchServerImage.ps1", warnings, output);
        WriteIncursaProjectStatus(root, warnings, output);
        WriteAdapterBackedManifestStatus(root, warnings, output);

        output.WriteLine();
        WriteScenarioValidationSummary(root, warnings, output);

        output.WriteLine();
        output.WriteLine("Warnings and remediation:");
        if (warnings.Count == 0)
        {
            output.WriteLine("  none");
        }
        else
        {
            foreach (var warning in warnings.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                output.WriteLine($"  - {warning}");
            }
        }

        output.WriteLine();
        output.WriteLine("Next commands:");
        output.WriteLine("  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\\bootstrap\\Initialize-ProtocolLab.ps1 -BuildH2LoadImage");
        output.WriteLine("  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\\bootstrap\\Initialize-ProtocolLab.ps1 -BuildTargetImages -BuildIncursaTargetImage -SkipCheck");
        output.WriteLine("  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\\build\\Build-CaddyBenchServerImage.ps1");
        output.WriteLine("  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\\build\\Build-NginxBenchServerImage.ps1");
        output.WriteLine("  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\\acceptance\\Invoke-ProtocolLabAcceptance.ps1 -RunIdPrefix local-v1-acceptance -DurationSeconds 5 -WarmupSeconds 1 -Repetitions 1");
        output.WriteLine("  dotnet run --project src\\Incursa.ProtocolLab.Cli -- validate --implementations kestrel-http3,incursa-http3 --target-mode docker --scenarios http.core.plaintext,http.core.json --protocol h3");
        output.WriteLine("  dotnet run --project src\\Incursa.ProtocolLab.Cli -- validate --implementations caddy-http3 --target-mode docker --target-network-mode shared-docker-network --scenarios http.core.plaintext,http.core.json --protocol h3");
        output.WriteLine("  dotnet run --project src\\Incursa.ProtocolLab.Cli -- validate --implementations nginx-http3 --target-mode docker --target-network-mode shared-docker-network --scenarios http.core.plaintext,http.core.json --protocol h3");
        return RunnerCommandResult.Create(RunnerCommandKind.Check, 0, output.Messages);
    }

    public RunnerCommandResult List(string[] args, string root, IRunnerEventSink? eventSink = null)
    {
        var output = new RunnerOutputBuffer(RunnerCommandKind.List, eventSink);
        if (args.Length == 0)
        {
            output.WriteError("Specify 'list implementations', 'list scenarios', 'list network-profiles', or 'list load-tools'.");
            return RunnerCommandResult.Create(RunnerCommandKind.List, 1, output.Messages);
        }

        var target = args[0].ToLowerInvariant();
        if (target == "implementations")
        {
            foreach (var manifest in ManifestCatalog.Load(Path.Combine(root, "implementations")))
            {
                output.WriteLine($"{manifest.Id}\t{manifest.Name}\tkind={manifest.TargetKind}\tcontract={(string.IsNullOrWhiteSpace(manifest.TargetContract) ? "target" : manifest.TargetContract)}");
            }

            return RunnerCommandResult.Create(RunnerCommandKind.List, 0, output.Messages);
        }

        if (target == "scenarios")
        {
            var scenarios = ScenarioCatalog.Load(Path.Combine(root, "scenarios"));
            var protocols = scenarios
                .Select(static s => s.Protocol)
                .Where(static p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static p => p);

            foreach (var protocol in protocols)
            {
                output.WriteLine($"--- {protocol} ---");
                foreach (var scenario in scenarios.Where(s => string.Equals(s.Protocol, protocol, StringComparison.OrdinalIgnoreCase)))
                {
                    var status = !string.IsNullOrWhiteSpace(scenario.Status) ? scenario.Status : "draft";
                    var kind = !string.IsNullOrWhiteSpace(scenario.Kind) ? scenario.Kind : "workload";
                    var layer = !string.IsNullOrWhiteSpace(scenario.Layer) ? scenario.Layer : "application";
                    var shape = !string.IsNullOrWhiteSpace(scenario.TrafficShape) ? scenario.TrafficShape : "request-response";
                    var caps = string.Join(",", scenario.GetEffectiveRequiredCapabilities());
                    var statusLabel = string.Empty;
                    if (scenario.IsPlaceholder())
                    {
                        statusLabel = " [placeholder]";
                    }
                    else if (scenario.IsExperimental())
                    {
                        statusLabel = " [experimental]";
                    }

                    output.WriteLine($"{scenario.Id}\t{scenario.GetTitle()}\tstatus={status}{statusLabel}\tkind={kind}\tlayer={layer}\tprotocol={protocol}\tshape={shape}\tcaps={caps}");
                }

                output.WriteLine();
            }

            return RunnerCommandResult.Create(RunnerCommandKind.List, 0, output.Messages);
        }

        if (target is "network-profiles" or "profiles")
        {
            foreach (var profile in NetworkProfileCatalog.Load(Path.Combine(root, "scenarios", "network", "profiles")))
            {
                output.WriteLine($"{profile.Id}\t{profile.Name}\t{profile.Provider}");
            }

            return RunnerCommandResult.Create(RunnerCommandKind.List, 0, output.Messages);
        }

        if (target is "load-tools" or "loadtools")
        {
            foreach (var tool in LoadToolCatalog.Load(Path.Combine(root, "load-tools")))
            {
                output.WriteLine($"{tool.Id}\t{tool.Name}\t{tool.Kind}");
            }

            return RunnerCommandResult.Create(RunnerCommandKind.List, 0, output.Messages);
        }

        output.WriteError($"Unknown list target '{target}'.");
        return RunnerCommandResult.Create(RunnerCommandKind.List, 1, output.Messages);
    }

    public async Task<RunnerCommandResult> ValidateAsync(string root, RunnerCommandOptions options, IRunnerEventSink? eventSink = null)
    {
        var output = new RunnerOutputBuffer(RunnerCommandKind.Validate, eventSink);
        var executionProfile = BuildExecutionProfile(options);
        var cells = LoadCells(root, options)
            .Select(cell => cell with { ExecutionProfile = executionProfile })
            .ToArray();
        var externalBaseUrl = options.Get("base-url");
        var outputDirectory = options.Get("output") ?? Path.Combine(root, ".artifacts", "runs");
        var runId = options.Get("run-id") ?? $"validate-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var targetOptions = BuildTargetStartOptions(options, runId);
        var networkProfiles = NetworkProfileCatalog.Load(Path.Combine(root, "scenarios", "network", "profiles"));
        var loadProfiles = LoadProfileCatalog.Load(Path.Combine(root, "load-profiles"));
        var results = new List<ScenarioValidationResult>();

        foreach (var cell in cells)
        {
            var paths = ArtifactLayout.GetCellPaths(outputDirectory, runId, cell);
            PrepareCellDirectories(paths);
            await WriteSnapshotsAsync(paths, cell);

            var compatibility = CompatibilityClassifier.Classify(cell, networkProfiles: networkProfiles, loadProfiles: loadProfiles);
            if (!compatibility.CanRun)
            {
                var compatibilityValidation = BuildCompatibilityValidationResult(cell, compatibility);
                await File.WriteAllTextAsync(paths.ValidationJson, ResultJson.Serialize(compatibilityValidation));
                await TargetOrchestrator.WriteTargetExecutionAsync(paths, BuildCompatibilityTargetResult(paths, compatibility));
                results.Add(compatibilityValidation);
                output.WriteLine($"{cell.Implementation.Id}/{cell.Scenario.Id}/{cell.Protocol}: {compatibilityValidation.Status} - {compatibilityValidation.Summary}");
                continue;
            }

            var target = await TargetOrchestrator.StartAsync(root, cell.Implementation, externalBaseUrl, paths, cell.Protocol, targetOptions);
            AdapterSessionHandle? adapterSession = null;
            var executionTarget = target.Result;
            var targetBaseUrl = target.BaseUrl;

            ScenarioValidationResult validation;
            try
            {
                if (string.Equals(cell.Implementation.TargetContract, "adapter-v1", StringComparison.OrdinalIgnoreCase) &&
                    !target.Result.Unsupported &&
                    !target.Result.Failed &&
                    target.Result.Ready)
                {
                    adapterSession = await AdapterSessionOrchestrator.StartAsync(
                        root,
                        cell,
                        target.BaseUrl,
                        paths,
                        runId,
                        BuildCellId(cell),
                        target.Result);
                    executionTarget = adapterSession.Result;
                    targetBaseUrl = adapterSession.ProtocolBaseUrl ?? adapterSession.Result.TargetEffectiveBaseUrl ?? target.BaseUrl;
                }

                validation = await ValidateCellAsync(root, cell, targetBaseUrl, executionTarget, paths);
                await File.WriteAllTextAsync(paths.ValidationJson, ResultJson.Serialize(validation));
            }
            finally
            {
                if (adapterSession is not null)
                {
                    await adapterSession.DisposeAsync();
                    executionTarget = adapterSession.Result;
                }

                await target.DisposeAsync();
                await TargetOrchestrator.WriteTargetExecutionAsync(paths, executionTarget);
            }

            results.Add(validation);
            output.WriteLine($"{cell.Implementation.Id}/{cell.Scenario.Id}/{cell.Protocol}: {validation.Status} - {validation.Summary}");
        }

        var runRoot = ArtifactLayout.GetRunRoot(outputDirectory, runId);
        Directory.CreateDirectory(runRoot);
        var validationResultsPath = Path.Combine(runRoot, "validation-results.json");
        await File.WriteAllTextAsync(validationResultsPath, ResultJson.Serialize(results));

        return RunnerCommandResult.Create(
            RunnerCommandKind.Validate,
            results.Any(result => result.Status == ValidationStatus.Failed) ? 1 : 0,
            output.Messages,
            [new RunnerArtifactReference("validation-results", validationResultsPath)]);
    }

    public async Task<RunnerCommandResult> RunBenchmarkAsync(string root, RunnerCommandOptions options, IRunnerEventSink? eventSink = null)
    {
        var output = new RunnerOutputBuffer(RunnerCommandKind.Run, eventSink);
        var executionProfile = BuildExecutionProfile(options);
        var cells = LoadCells(root, options)
            .Select(cell => cell with { ExecutionProfile = executionProfile })
            .ToArray();
        var externalBaseUrl = options.Get("base-url");
        var outputDirectory = options.Get("output") ?? Path.Combine(root, ".artifacts", "runs");
        var runId = options.Get("run-id") ?? $"local-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var targetOptions = BuildTargetStartOptions(options, runId);
        var loadToolDockerResourceLimits = BuildLoadToolDockerResourceLimits(options);
        var captureLoadToolMetrics = string.Equals(options.Get("capture-load-tool-metrics"), "true", StringComparison.OrdinalIgnoreCase);
        var captureLoadToolQlog = !string.Equals(options.Get("disable-load-tool-qlog"), "true", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.Get("capture-load-tool-qlog"), "false", StringComparison.OrdinalIgnoreCase);
        var loadToolMetricsInterval = TimeSpan.FromSeconds(Math.Max(1, ParseInt(options.Get("load-tool-metrics-interval")) ?? ParseInt(options.Get("load-tool-metrics-interval-seconds")) ?? 1));
        var captureTargetContainerMetrics = string.Equals(options.Get("capture-target-container-metrics"), "true", StringComparison.OrdinalIgnoreCase);
        var targetContainerMetricsInterval = TimeSpan.FromSeconds(Math.Max(1, ParseInt(options.Get("target-container-metrics-interval")) ?? ParseInt(options.Get("target-container-metrics-interval-seconds")) ?? 1));
        var requestedLoadTool = options.Get("load-tool");
        var requestedLoadToolMode = options.Get("load-tool-mode");
        var networkProfiles = NetworkProfileCatalog.Load(Path.Combine(root, "scenarios", "network", "profiles"));
        var loadProfiles = LoadProfileCatalog.Load(Path.Combine(root, "load-profiles"));
        var counterOptions = new RuntimeCounterCaptureOptions(
            string.Equals(options.Get("capture-counters"), "true", StringComparison.OrdinalIgnoreCase),
            options.Get("counter-tool") ?? "dotnet-counters",
            Math.Max(1, ParseInt(options.Get("counter-refresh-interval")) ?? 1),
            options.Get("counter-format") ?? "json",
            root);
        var loadTools = LoadToolCatalog.Load(Path.Combine(root, "load-tools"));
        var metadata = await RunMetadataCapture.CaptureAsync(executionProfile);
        var results = new List<BenchmarkResult>();
        var compatibilities = new List<RunCellCompatibility>();
        var totalRepetitionsByCell = cells
            .GroupBy(GetRepetitionGroupKey)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        foreach (var cell in cells)
        {
            var totalRepetitions = totalRepetitionsByCell[GetRepetitionGroupKey(cell)];
            var paths = ArtifactLayout.GetCellPaths(outputDirectory, runId, cell);
            PrepareCellDirectories(paths);
            await WriteSnapshotsAsync(paths, cell);

            var artifacts = BuildArtifactMap(paths);
            var warnings = new List<string>();
            var errors = new List<string>();
            var loadShapeWarnings = new List<string>();
            string? recordedLoadTool = null;
            string? recordedLoadToolMode = null;
            string? recordedLoadToolCategory = null;
            string? recordedLoadToolVersion = null;
            var benchmarkExecutionStatus = LoadToolExecutionStatuses.Skipped;
            string? benchmarkFailureReason = null;
            var parsedMetricsAvailable = false;
            var metrics = new HttpMetrics();
            RequestedLoadShape? requestedLoadShape = null;
            EffectiveLoadShape? effectiveLoadShape = null;
            LoadShapeSemantics? loadShapeSemantics = null;
            string? loadToolCommandLine = null;
            string? dockerImage = null;
            string? dockerCommandLine = null;
            string? loadToolDockerInspectPath = null;
            string? loadToolContainerId = null;
            string? loadToolContainerName = null;
            DockerResourceLimits? loadToolResourceLimitsRequested = null;
            DockerResourceLimits? loadToolResourceLimitsEffective = null;
            DockerContainerMetricsSummary? loadToolDockerMetricsSummary = null;
            var loadToolDockerMetricsAvailable = false;
            string? loadToolSaturationStatus = null;
            IReadOnlyList<string> loadToolSaturationWarnings = [];
            Dictionary<string, string> loadToolDockerMetricsArtifacts = [];
            DockerCleanupSummary? loadToolCleanupSummary = null;
            string? loadToolWorkingDirectory = null;
            string? loadToolParserId = null;
            string? requestedLoadToolUrl = null;
            string? effectiveLoadToolUrl = null;
            string? loadToolConnectTarget = null;
            string? hostRewriteMode = null;
            string? loadToolSni = null;
            string? loadToolContainerNetwork = null;
            string? loadToolCertificateMode = null;
            TargetProcessMetrics? targetProcessMetrics = null;
            TargetDockerMetricsSummary? targetDockerMetricsSummary = null;
            var targetDockerMetricsAvailable = false;
            string? targetSaturationStatus = null;
            IReadOnlyList<string> targetSaturationWarnings = [];
            Dictionary<string, string> targetDockerMetricsArtifacts = [];
            DockerCleanupSummary? targetMetricsCleanupSummary = null;
            DiagnosticTarget? diagnosticTarget = null;
            var countersAvailable = false;
            var countersCaptureStatus = counterOptions.Enabled ? CounterCaptureStatuses.NotRun : CounterCaptureStatuses.Disabled;
            RuntimeCounterSummary? countersSummary = null;
            Dictionary<string, string> counterArtifacts = [];
            int? qlogFileCount = null;
            bool? targetProcessExitedBeforeDispose = null;
            var loadToolExecution = new LoadToolExecutionResult
            {
                Status = LoadToolExecutionStatuses.Skipped,
                StdoutPath = paths.LoadToolStdout,
                StderrPath = paths.LoadToolStderr
            };
            var compatibility = CompatibilityClassifier.Classify(cell, networkProfiles: networkProfiles, loadProfiles: loadProfiles);
            compatibilities.Add(compatibility);
            if (!compatibility.CanRun)
            {
                var reason = compatibility.Reason ?? $"Run cell is {compatibility.Status}.";
                var compatibilityValidation = BuildCompatibilityValidationResult(cell, compatibility);
                var targetResult = BuildCompatibilityTargetResult(paths, compatibility);
                var compatibilityWarning = $"{compatibility.Status}: {reason}";
                warnings.Add(compatibilityWarning);
                warnings.Add("Benchmark was not accepted because validation did not pass.");

                await File.WriteAllTextAsync(paths.ValidationJson, ResultJson.Serialize(compatibilityValidation));
                await TargetOrchestrator.WriteTargetExecutionAsync(paths, targetResult);
                await File.WriteAllTextAsync(paths.LoadToolStdout, "");
                await File.WriteAllTextAsync(paths.LoadToolStderr, reason);

                loadToolExecution = loadToolExecution with
                {
                    Status = LoadToolExecutionStatuses.Skipped,
                    Errors = [reason],
                    Warnings = warnings
                };
                await File.WriteAllTextAsync(paths.LoadToolExecutionJson, ResultJson.Serialize(loadToolExecution));
                await WriteNotesAsync(paths, warnings, errors);

                var skippedResult = BenchmarkResult.FromCell(
                    runId,
                    cell,
                    compatibilityValidation,
                    requestedLoadTool,
                    parsedMetricsAvailable,
                    artifacts,
                    metrics,
                    warnings,
                    errors,
                    benchmarkExecutionStatus: LoadToolExecutionStatuses.Skipped,
                    benchmarkFailureReason: reason,
                    targetExecution: targetResult);
                skippedResult = skippedResult with
                {
                    Evidence = BenchmarkEvidenceEvaluator.Assess(skippedResult)
                };
                skippedResult = PopulateLoadProfileMetadata(skippedResult, cell, loadProfiles);
                await File.WriteAllTextAsync(paths.ResultJson, ResultJson.Serialize(skippedResult));
                results.Add(skippedResult);
                output.WriteLine($"{cell.Implementation.Id}/{cell.Scenario.Id}/{cell.Protocol}: validation={compatibilityValidation.Status}, benchmark={LoadToolExecutionStatuses.Skipped}, loadTool=none");
                continue;
            }

            var target = await TargetOrchestrator.StartAsync(root, cell.Implementation, externalBaseUrl, paths, cell.Protocol, targetOptions);
            AdapterSessionHandle? adapterSession = null;
            var executionTarget = target.Result;
            var targetBaseUrl = target.BaseUrl;
            var isAdapterTarget = string.Equals(cell.Implementation.TargetContract, "adapter-v1", StringComparison.OrdinalIgnoreCase);

            ScenarioValidationResult validation;

            try
            {
                if (isAdapterTarget &&
                    !target.Result.Unsupported &&
                    !target.Result.Failed &&
                    target.Result.Ready)
                {
                    adapterSession = await AdapterSessionOrchestrator.StartAsync(
                        root,
                        cell,
                        target.BaseUrl,
                        paths,
                        runId,
                        BuildCellId(cell),
                        target.Result);
                    executionTarget = adapterSession.Result;
                    targetBaseUrl = adapterSession.ProtocolBaseUrl ?? executionTarget.TargetEffectiveBaseUrl ?? target.BaseUrl;
                }

                validation = await ValidateCellAsync(root, cell, targetBaseUrl, executionTarget, paths);
                await File.WriteAllTextAsync(paths.ValidationJson, ResultJson.Serialize(validation));
                warnings.AddRange(validation.Warnings ?? []);
                errors.AddRange(validation.Errors ?? []);
                diagnosticTarget = DiagnosticTargetResolver.Resolve(isAdapterTarget ? null : target.Process, executionTarget);
                await File.WriteAllTextAsync(paths.DiagnosticTargetJson, ResultJson.Serialize(diagnosticTarget));
                if (string.Equals(diagnosticTarget.Confidence, DiagnosticTargetConfidenceLevels.Low, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add("diagnostic-target-low-confidence.");
                }

                warnings.AddRange(diagnosticTarget.Warnings);

                if (validation.AllowsBenchmark)
                {
                    var resolution = LoadToolInvoker.Resolve(loadTools, cell, requestedLoadTool, requestedLoadToolMode);
                    loadToolExecution = resolution.Result with
                    {
                        StdoutPath = paths.LoadToolStdout,
                        StderrPath = paths.LoadToolStderr
                    };

                    if (!resolution.CanExecute || resolution.Tool is null)
                    {
                        benchmarkExecutionStatus = loadToolExecution.Status;
                        benchmarkFailureReason = loadToolExecution.Errors.FirstOrDefault() ?? loadToolExecution.Status;
                        recordedLoadTool = loadToolExecution.ToolId ?? requestedLoadTool;
                        recordedLoadToolMode = loadToolExecution.Mode ?? requestedLoadToolMode;
                        recordedLoadToolCategory = loadToolExecution.Category;
                        warnings.Add($"{benchmarkExecutionStatus}: {benchmarkFailureReason}");
                        await File.WriteAllTextAsync(paths.LoadToolStdout, "");
                        await File.WriteAllTextAsync(paths.LoadToolStderr, $"{benchmarkExecutionStatus}: {benchmarkFailureReason}");
                    }
                    else
                    {
                        recordedLoadTool = resolution.Tool.Manifest.Id;
                        recordedLoadToolMode = resolution.Tool.Mode;
                        recordedLoadToolCategory = resolution.Tool.Manifest.Category;
                        dockerImage = resolution.Tool.DockerImage;
                        var version = await LoadToolInvoker.CaptureVersionAsync(resolution.Tool);
                        recordedLoadToolVersion = version;
                        await File.WriteAllTextAsync(paths.LoadToolVersion, version ?? "");

                        var capabilityResult = await ValidateH3LoadToolCapabilityAsync(cell, resolution.Tool);
                        var loadToolCapability = capabilityResult.Capability;
                        if (loadToolCapability is not null)
                        {
                            loadToolExecution = loadToolExecution with
                            {
                                H3CapabilityStatus = loadToolCapability.Status,
                                H3CapabilityWarnings = loadToolCapability.Warnings ?? []
                            };
                        }

                        if (capabilityResult.Error is not null)
                        {
                            benchmarkExecutionStatus = LoadToolExecutionStatuses.Unavailable;
                            benchmarkFailureReason = capabilityResult.Error;
                            warnings.Add($"{benchmarkExecutionStatus}: {benchmarkFailureReason}");
                            await File.WriteAllTextAsync(paths.LoadToolStdout, "");
                            await File.WriteAllTextAsync(paths.LoadToolStderr, benchmarkFailureReason);
                            loadToolExecution = loadToolExecution with
                            {
                                Status = benchmarkExecutionStatus,
                                ToolId = resolution.Tool.Manifest.Id,
                                ToolName = resolution.Tool.Manifest.Name,
                                Mode = resolution.Tool.Mode,
                                Category = resolution.Tool.Manifest.Category,
                                ExecutablePath = resolution.Tool.ExecutablePath,
                                DockerImage = resolution.Tool.DockerImage,
                                Version = version,
                                H3CapabilityStatus = loadToolCapability?.Status ?? loadToolExecution.H3CapabilityStatus,
                                H3CapabilityWarnings = loadToolCapability?.Warnings ?? loadToolExecution.H3CapabilityWarnings,
                                StdoutPath = paths.LoadToolStdout,
                                StderrPath = paths.LoadToolStderr,
                                Errors = [benchmarkFailureReason]
                            };
                        }
                        else
                        {
                            var targetUrl = cell.Scenario.Endpoint is null
                                ? new Uri(targetBaseUrl.TrimEnd('/') + "/")
                                : HttpScenarioValidator.BuildScenarioUri(targetBaseUrl, cell.Scenario.Endpoint);
                            var plan = LoadToolInvoker.BuildExecutionPlan(
                                resolution.Tool,
                                targetUrl,
                                cell,
                                paths,
                                executionTarget,
                                loadToolDockerResourceLimits,
                                captureLoadToolMetrics,
                                loadToolMetricsInterval,
                                captureLoadToolQlog);
                            if (!captureLoadToolQlog)
                            {
                                warnings.Add("load-tool-qlog-disabled.");
                            }
                            requestedLoadShape = plan.RequestedLoadShape;
                            effectiveLoadShape = plan.EffectiveLoadShape;
                            loadShapeSemantics = plan.Semantics;
                            loadShapeWarnings.AddRange(plan.Semantics.Warnings);
                            loadShapeWarnings.AddRange(plan.EffectiveLoadShape.Warnings);
                            if (string.Equals(resolution.Tool.Manifest.Category, LoadToolCategories.ManagedLab, StringComparison.OrdinalIgnoreCase))
                            {
                                warnings.Add("managed-httpclient-h3-load results are valid local lab measurements, not external-reference h2load benchmarks.");
                            }
                            loadToolCommandLine = plan.CommandLine;
                            dockerImage = resolution.Tool.DockerImage;
                            dockerCommandLine = plan.DockerCommandLine;
                            loadToolWorkingDirectory = plan.WorkingDirectory;
                            loadToolParserId = resolution.Tool.Manifest.GetEffectiveParserType();

                            requestedLoadToolUrl = plan.RequestedTargetUrl.ToString();
                            effectiveLoadToolUrl = plan.TargetUrl.ToString();
                            loadToolConnectTarget = plan.ConnectTarget;
                            hostRewriteMode = plan.HostRewriteMode;
                            loadToolSni = plan.Sni;
                            loadToolContainerNetwork = plan.DockerNetwork;
                            loadToolCertificateMode = plan.CertificateMode;
                            if (plan.HostRewriteMode is not null)
                            {
                                warnings.Add(string.Equals(plan.HostRewriteMode, "shared-docker-network-connect-to", StringComparison.OrdinalIgnoreCase)
                                    ? "Docker load tool uses shared-docker-network connect-to routing with SNI preserved for local certificate compatibility."
                                    : $"Docker load tool target URL was rewritten for host access: {plan.HostRewriteMode}.");
                            }

                            string? loadToolDockerImageId = null;
                            string? loadToolDockerImageDigest = null;
                            if (string.Equals(resolution.Tool.Mode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase))
                            {
                                warnings.AddRange(DockerResourceControl.BuildWarnings(plan.DockerResourceLimits, effective: null, targetContainer: false));
                                if (!string.IsNullOrWhiteSpace(plan.DockerNetwork))
                                {
                                    warnings.Add(BenchmarkEvidenceReasons.SharedDockerNetwork);
                                    warnings.Add(BenchmarkEvidenceReasons.DockerNetworkLocal);
                                    warnings.Add(BenchmarkEvidenceReasons.CertificateSniConnectToRouting);
                                }

                                var imageMetadata = await LoadToolInvoker.CaptureDockerImageMetadataAsync(resolution.Tool);
                                loadToolDockerImageId = imageMetadata.ImageId;
                                loadToolDockerImageDigest = imageMetadata.ImageDigest;
                                warnings.AddRange(imageMetadata.Warnings);
                            }

                            ProcessMetricSnapshot? targetMetricsBefore = null;
                            ProcessMetricSnapshot? targetMetricsAfter = null;
                            IReadOnlyList<ProcessMetricSample> targetMetricSamples = [];
                            var targetProcess = isAdapterTarget ? null : target.Process;
                            CancellationTokenSource? targetMetricsCts = null;
                            Task<IReadOnlyList<ProcessMetricSample>>? targetMetricsTask = null;
                            CancellationTokenSource? targetContainerMetricsCts = null;
                            Task<DockerMetricsSamplingResult>? targetContainerMetricsTask = null;
                            var counterSession = RuntimeCounterSession.Disabled(paths);

                            if (targetProcess is not null)
                            {
                                targetMetricsBefore = ProcessMetricsCapture.CaptureSnapshot(targetProcess);
                                if (!targetProcess.HasExited)
                                {
                                    targetMetricsCts = new CancellationTokenSource();
                                    targetMetricsTask = ProcessMetricsCapture.SampleAsync(targetProcess, targetMetricsCts.Token, TimeSpan.FromSeconds(1));
                                }
                            }
                            else if (validation.AllowsBenchmark)
                            {
                                warnings.Add("Target process metrics were not captured because the target was external, prestarted, or containerized.");
                            }

                            if (captureTargetContainerMetrics &&
                                string.Equals(executionTarget.TargetExecutionMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrWhiteSpace(executionTarget.TargetContainerName))
                            {
                                var docker = LoadToolInvoker.ResolveExecutable("docker");
                                if (docker is null)
                                {
                                    warnings.Add($"{BenchmarkEvidenceReasons.DockerStatsUnavailable}: Docker executable was not found for target container metrics.");
                                }
                                else
                                {
                                    targetContainerMetricsCts = new CancellationTokenSource();
                                    targetContainerMetricsTask = DockerContainerMetricsCapture.CaptureUntilCanceledAsync(
                                        docker,
                                        executionTarget.TargetContainerName,
                                        paths.TargetDockerStatsRawTxt,
                                        paths.TargetDockerStatsJsonl,
                                        paths.TargetDockerMetricsSummaryJson,
                                        targetContainerMetricsInterval,
                                        targetContainerMetricsCts.Token);
                                }
                            }

                            LoadToolRun? run = null;
                            var startTime = DateTimeOffset.UtcNow;
                            try
                            {
                                if (counterOptions.Enabled)
                                {
                                    counterSession = await RuntimeCounterSession.StartAsync(
                                        counterOptions,
                                        diagnosticTarget,
                                        paths,
                                        cell.DurationSeconds + cell.WarmupSeconds + 3);
                                }

                                run = await LoadToolInvoker.RunAsync(resolution.Tool, plan);
                            }
                            finally
                            {
                                targetMetricsCts?.Cancel();
                                targetContainerMetricsCts?.Cancel();
                            }

                            var endTime = DateTimeOffset.UtcNow;
                            var counterResult = await counterSession.StopAsync();
                            countersAvailable = counterResult.CountersAvailable;
                            countersCaptureStatus = counterResult.Status;
                            countersSummary = counterResult.Summary;
                            counterArtifacts = counterResult.Artifacts;
                            warnings.AddRange(counterResult.Warnings);
                            warnings.AddRange(counterResult.Errors);
                            if (counterOptions.Enabled && !string.Equals(counterResult.Status, CounterCaptureStatuses.Succeeded, StringComparison.OrdinalIgnoreCase))
                            {
                                warnings.Add("counter-capture-failed-or-unavailable.");
                            }

                            if (run is null)
                            {
                                throw new InvalidOperationException($"Load tool '{resolution.Tool.Manifest.Id}' did not return a run result.");
                            }

                            await File.WriteAllTextAsync(paths.LoadToolStdout, run.Stdout);
                            await File.WriteAllTextAsync(paths.LoadToolStderr, run.Stderr);
                            loadToolDockerInspectPath = run.DockerInspectPath;
                            loadToolContainerId = run.ContainerId;
                            loadToolContainerName = run.ContainerName;
                            loadToolResourceLimitsRequested = run.DockerResourceLimitsRequested;
                            loadToolResourceLimitsEffective = run.DockerResourceLimitsEffective;
                            loadToolDockerMetricsAvailable = run.DockerMetricsAvailable;
                            loadToolDockerMetricsSummary = run.DockerMetricsSummary;
                            loadToolSaturationStatus = run.SaturationStatus;
                            loadToolSaturationWarnings = run.SaturationWarnings;
                            loadToolDockerMetricsArtifacts = run.DockerMetricsArtifacts;
                            loadToolCleanupSummary = run.CleanupSummary;
                            warnings.AddRange(run.ResourceLimitWarnings);
                            warnings.AddRange(run.SaturationWarnings);
                            if (string.Equals(resolution.Tool.Manifest.Id, "h2load", StringComparison.OrdinalIgnoreCase))
                            {
                                await File.WriteAllTextAsync(paths.H2loadStdout, run.Stdout);
                                await File.WriteAllTextAsync(paths.H2loadStderr, run.Stderr);
                                await File.WriteAllTextAsync(paths.H2loadCommandTxt, plan.CommandLine);
                                await File.WriteAllTextAsync(paths.DockerCommandTxt, plan.DockerCommandLine ?? "");
                            }

                            if (targetMetricsTask is not null)
                            {
                                try
                                {
                                    targetMetricSamples = await targetMetricsTask;
                                }
                                catch
                                {
                                    targetMetricSamples = [];
                                }
                            }

                            if (targetProcess is not null)
                            {
                                targetMetricsAfter = ProcessMetricsCapture.CaptureSnapshot(targetProcess);
                                targetProcessMetrics = ProcessMetricsCapture.BuildTargetProcessMetrics(
                                    executionTarget,
                                    targetMetricsBefore,
                                    targetMetricsAfter,
                                    targetMetricSamples,
                                    endTime);
                                targetProcessExitedBeforeDispose = targetProcess.HasExited;
                            }

                            if (targetContainerMetricsTask is not null)
                            {
                                try
                                {
                                    var targetMetricCapture = await targetContainerMetricsTask;
                                    targetDockerMetricsSummary = DockerContainerMetricsCapture.ToTargetSummary(targetMetricCapture.Summary);
                                    targetDockerMetricsAvailable = targetDockerMetricsSummary.Samples.Count > 0;
                                    targetDockerMetricsArtifacts = targetMetricCapture.Artifacts;
                                    var saturation = DockerContainerMetricsCapture.AssessTargetSaturation(
                                        targetDockerMetricsSummary,
                                        executionTarget.TargetDockerResourceLimitsRequested,
                                        executionTarget.TargetDockerResourceLimitsEffective);
                                    targetSaturationStatus = saturation.Status;
                                    targetSaturationWarnings = targetMetricCapture.Warnings
                                        .Concat(saturation.Warnings)
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .ToArray();
                                    warnings.AddRange(targetSaturationWarnings);
                                    warnings.Add(targetDockerMetricsAvailable
                                        ? BenchmarkEvidenceReasons.TargetContainerMetricsCaptured
                                        : BenchmarkEvidenceReasons.TargetContainerMetricsMissing);
                                    targetMetricsCleanupSummary = new DockerCleanupSummary
                                    {
                                        TargetMetricsSamplerCleanupAttempted = true,
                                        TargetMetricsSamplerCleanupSucceeded = targetMetricCapture.SamplerStopped,
                                        Warnings = targetSaturationWarnings
                                    };
                                }
                                catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException)
                                {
                                    targetDockerMetricsAvailable = false;
                                    targetSaturationStatus = TargetSaturationStatuses.Unknown;
                                    targetSaturationWarnings = [BenchmarkEvidenceReasons.TargetContainerMetricsMissing, $"target-metrics-sampler-failed: {ex.Message}"];
                                    warnings.AddRange(targetSaturationWarnings);
                                    targetMetricsCleanupSummary = new DockerCleanupSummary
                                    {
                                        TargetMetricsSamplerCleanupAttempted = true,
                                        TargetMetricsSamplerCleanupSucceeded = false,
                                        Warnings = targetSaturationWarnings
                                    };
                                }
                            }

                            qlogFileCount = CountFiles(paths.QlogDirectory);

                            if (run.ExitCode != 0)
                            {
                                benchmarkExecutionStatus = LoadToolExecutionStatuses.Failed;
                                benchmarkFailureReason = $"Load tool exited with code {run.ExitCode}.";
                                errors.Add($"Load tool exited with code {run.ExitCode}.");
                            }
                            else
                            {
                                benchmarkExecutionStatus = LoadToolExecutionStatuses.Succeeded;
                                var parseStdout = run.Stdout;
                                if (string.Equals(resolution.Tool.Manifest.Id, "h2load", StringComparison.OrdinalIgnoreCase) &&
                                    File.Exists(paths.H2loadOutputJson))
                                {
                                    var outputJson = await File.ReadAllTextAsync(paths.H2loadOutputJson);
                                    if (!string.IsNullOrWhiteSpace(outputJson))
                                    {
                                        parseStdout = outputJson + Environment.NewLine + run.Stdout;
                                    }
                                }

                                var parsed = LoadToolInvoker.Parse(resolution.Tool.Manifest, parseStdout, run.Stderr);
                                parsedMetricsAvailable = parsed.ParsedMetricsAvailable;
                                metrics = parsed.Metrics;
                                warnings.AddRange(parsed.Warnings);
                                if (!parsed.ParsedMetricsAvailable)
                                {
                                    loadShapeWarnings.Add("Benchmark completed but parsed metrics are unavailable; inspect raw load-tool stdout/stderr.");
                                }

                                if (string.Equals(resolution.Tool.Manifest.Id, "h2load", StringComparison.OrdinalIgnoreCase) &&
                                    qlogFileCount == 0)
                                {
                                    warnings.Add("h2load qlog artifacts were not produced for this benchmark cell; qlog review is deferred.");
                                }

                                if (string.Equals(resolution.Tool.Mode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (run.DockerMetricsAvailable)
                                    {
                                        warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorMetricsCaptured);
                                    }
                                    else if (captureLoadToolMetrics)
                                    {
                                        warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorMetricsMissing);
                                    }
                                    else
                                    {
                                        warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorCpuNotCaptured);
                                    }
                                }
                                else
                                {
                                    warnings.Add(BenchmarkEvidenceReasons.LoadGeneratorCpuNotCaptured);
                                }
                                if (resolution.Tool.Manifest.Category is not null &&
                                    string.Equals(resolution.Tool.Manifest.Category, LoadToolCategories.ManagedLab, StringComparison.OrdinalIgnoreCase))
                                {
                                    warnings.Add("managed-httpclient-h3-load results are valid local lab measurements, not external-reference h2load benchmarks.");
                                }
                                if (run.Stderr.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                                    run.Stderr.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                                    run.Stderr.Contains("overload", StringComparison.OrdinalIgnoreCase) ||
                                    run.Stderr.Contains("connection", StringComparison.OrdinalIgnoreCase))
                                {
                                    warnings.Add("load-generator-stderr-suggests-overload-or-connection-pressure.");
                                }
                            }

                            if (targetProcessMetrics is null &&
                                benchmarkExecutionStatus == LoadToolExecutionStatuses.Succeeded &&
                                !targetDockerMetricsAvailable)
                            {
                                warnings.Add("Target process metrics were not captured for this benchmark cell.");
                            }

                            loadToolExecution = loadToolExecution with
                            {
                                Status = benchmarkExecutionStatus,
                                ToolId = resolution.Tool.Manifest.Id,
                                ToolName = resolution.Tool.Manifest.Name,
                                Mode = resolution.Tool.Mode,
                                Category = resolution.Tool.Manifest.Category,
                                ExecutablePath = resolution.Tool.ExecutablePath,
                                DockerImage = resolution.Tool.DockerImage,
                                DockerImageId = loadToolDockerImageId,
                                DockerImageDigest = loadToolDockerImageDigest,
                                ContainerId = loadToolContainerId,
                                ContainerName = loadToolContainerName,
                                DockerInspectPath = loadToolDockerInspectPath,
                                LoadToolDockerResourceLimitsRequested = loadToolResourceLimitsRequested,
                                LoadToolDockerResourceLimitsEffective = loadToolResourceLimitsEffective,
                                ResourceLimitWarnings = run.ResourceLimitWarnings,
                                LoadToolDockerMetricsAvailable = loadToolDockerMetricsAvailable,
                                LoadToolDockerMetricsSummary = loadToolDockerMetricsSummary,
                                LoadToolSaturationStatus = loadToolSaturationStatus,
                                LoadToolSaturationWarnings = loadToolSaturationWarnings,
                                LoadToolDockerMetricsArtifacts = loadToolDockerMetricsArtifacts,
                                CleanupSummary = loadToolCleanupSummary,
                                Version = version,
                                CommandLine = plan.CommandLine,
                                DockerCommandLine = plan.DockerCommandLine,
                                WorkingDirectory = plan.WorkingDirectory,
                                ParserId = resolution.Tool.Manifest.GetEffectiveParserType(),
                                RequestedUrl = plan.RequestedTargetUrl.ToString(),
                                EffectiveUrl = plan.TargetUrl.ToString(),
                                ConnectTarget = plan.ConnectTarget,
                                HostRewriteMode = plan.HostRewriteMode,
                                Sni = plan.Sni,
                                CertificateMode = plan.CertificateMode,
                                ContainerNetwork = plan.DockerNetwork,
                                ExitCode = run.ExitCode,
                                ContainerExitCode = string.Equals(resolution.Tool.Mode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase) ? run.ExitCode : null,
                                StartTimeUtc = startTime,
                                EndTimeUtc = endTime,
                                StdoutPath = paths.LoadToolStdout,
                                StderrPath = paths.LoadToolStderr,
                                H3CapabilityStatus = loadToolExecution.H3CapabilityStatus,
                                H3CapabilityWarnings = loadToolExecution.H3CapabilityWarnings,
                                Warnings = warnings.Concat(loadToolExecution.H3CapabilityWarnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                                Errors = benchmarkFailureReason is null ? [] : [benchmarkFailureReason]
                            };
                        }
                    }
                }
                else
                {
                    benchmarkExecutionStatus = LoadToolExecutionStatuses.Skipped;
                    benchmarkFailureReason = "Validation did not pass; load tool was not invoked.";
                    warnings.Add("Benchmark was not accepted because validation did not pass.");
                    await File.WriteAllTextAsync(paths.LoadToolStdout, "");
                    await File.WriteAllTextAsync(paths.LoadToolStderr, benchmarkFailureReason);
                    loadToolExecution = loadToolExecution with
                    {
                        Status = benchmarkExecutionStatus,
                        Errors = [benchmarkFailureReason]
                    };
                }
            }
            finally
            {
                if (adapterSession is not null)
                {
                    await adapterSession.DisposeAsync();
                    executionTarget = adapterSession.Result;
                }

                await target.DisposeAsync();
                await TargetOrchestrator.WriteTargetExecutionAsync(paths, executionTarget);
            }

            warnings.AddRange(executionTarget.Warnings);
            warnings.AddRange(executionTarget.NetworkWarnings);
            warnings.AddRange(executionTarget.ResourceLimitWarnings);

            if (targetProcessMetrics is not null)
            {
                targetProcessMetrics = targetProcessMetrics with
                {
                    ExitCode = executionTarget.ExitCode,
                    Crashed = targetProcessExitedBeforeDispose == true &&
                        executionTarget.ExitCode.HasValue &&
                        executionTarget.ExitCode.Value != 0
                };

                if (targetProcessMetrics.Crashed)
                {
                    var exitCode = executionTarget.ExitCode.GetValueOrDefault();
                    var exitWarning = $"target-process-exit-code-{exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                    targetProcessMetrics = targetProcessMetrics with
                    {
                        Warnings = targetProcessMetrics.Warnings
                            .Concat([exitWarning])
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray()
                    };
                }
            }

            if (requestedLoadShape is null)
            {
                requestedLoadShape = BuildRequestedLoadShape(cell);
            }

            if (effectiveLoadShape is null)
            {
                effectiveLoadShape = BuildSkippedEffectiveLoadShape(cell);
            }

            if (loadShapeSemantics is null)
            {
                loadShapeSemantics = new LoadShapeSemantics
                {
                    Protocol = cell.Protocol,
                    LoadTool = recordedLoadTool,
                    Warnings = []
                };
            }

            loadShapeWarnings.AddRange(BuildFairnessWarnings(
                cell,
                targetBaseUrl,
                totalRepetitions,
                benchmarkExecutionStatus == LoadToolExecutionStatuses.Succeeded ? parsedMetricsAvailable : null));
            var finalWarnings = FilterTargetDiagnosticWarnings(
                warnings,
                executionTarget,
                targetDockerMetricsAvailable)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var finalErrors = errors.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            loadToolExecution = loadToolExecution with
            {
                Warnings = finalWarnings.Concat(loadToolExecution.H3CapabilityWarnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
            await WriteNotesAsync(paths, finalWarnings, finalErrors);
            await File.WriteAllTextAsync(paths.LoadToolExecutionJson, ResultJson.Serialize(loadToolExecution));

            var result = BenchmarkResult.FromCell(
                runId,
                cell,
                validation,
                recordedLoadTool,
                parsedMetricsAvailable,
                artifacts,
                metrics,
                finalWarnings,
                finalErrors,
                recordedLoadToolMode,
                recordedLoadToolCategory,
                recordedLoadToolVersion,
                benchmarkExecutionStatus,
                benchmarkFailureReason,
                loadToolCommandLine,
                dockerImage,
                dockerCommandLine,
                loadToolWorkingDirectory,
                loadToolParserId,
                requestedLoadToolUrl,
                effectiveLoadToolUrl,
                loadToolConnectTarget,
                hostRewriteMode,
                loadToolSni,
                loadToolContainerNetwork,
                loadToolCertificateMode,
                requestedLoadShape,
                effectiveLoadShape,
                loadShapeSemantics,
                loadShapeWarnings.Distinct(StringComparer.OrdinalIgnoreCase),
                executionTarget,
                validation.ProtocolProof);

            result = result with
            {
                LoadToolExitCode = loadToolExecution.ExitCode,
                LoadToolH3CapabilityStatus = loadToolExecution.H3CapabilityStatus,
                LoadToolH3CapabilityWarnings = loadToolExecution.H3CapabilityWarnings.ToArray(),
                LoadToolDockerImageId = loadToolExecution.DockerImageId,
                LoadToolDockerImageDigest = loadToolExecution.DockerImageDigest,
                LoadToolContainerId = loadToolExecution.ContainerId,
                LoadToolDockerInspectPath = loadToolExecution.DockerInspectPath,
                LoadToolContainerName = loadToolExecution.ContainerName,
                LoadToolDockerResourceLimitsRequested = loadToolExecution.LoadToolDockerResourceLimitsRequested,
                LoadToolDockerResourceLimitsEffective = loadToolExecution.LoadToolDockerResourceLimitsEffective,
                LoadToolDockerMetricsAvailable = loadToolExecution.LoadToolDockerMetricsAvailable,
                LoadToolDockerMetricsSummary = loadToolExecution.LoadToolDockerMetricsSummary,
                LoadToolSaturationStatus = loadToolExecution.LoadToolSaturationStatus,
                LoadToolSaturationWarnings = loadToolExecution.LoadToolSaturationWarnings,
                LoadToolDockerMetricsArtifacts = loadToolExecution.LoadToolDockerMetricsArtifacts,
                TargetDockerMetricsAvailable = targetDockerMetricsAvailable,
                TargetDockerMetricsSummary = targetDockerMetricsSummary,
                TargetSaturationStatus = targetSaturationStatus,
                TargetSaturationWarnings = targetSaturationWarnings,
                TargetDockerMetricsArtifacts = targetDockerMetricsArtifacts,
                ResourceLimitWarnings = result.ResourceLimitWarnings
                    .Concat(loadToolExecution.ResourceLimitWarnings)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                DockerCleanup = MergeDockerCleanup(MergeDockerCleanup(result.DockerCleanup, loadToolExecution.CleanupSummary), targetMetricsCleanupSummary),
                TargetProcessMetrics = targetProcessMetrics,
                DiagnosticTarget = diagnosticTarget,
                CountersAvailable = countersAvailable,
                CountersCaptureStatus = countersCaptureStatus,
                CountersSummary = countersSummary,
                CounterArtifacts = counterArtifacts,
                QlogDirectory = paths.QlogDirectory,
                QlogFileCount = qlogFileCount
            };

            result = result with
            {
                Evidence = BenchmarkEvidenceEvaluator.Assess(result)
            };

            result = PopulateLoadProfileMetadata(result, cell, loadProfiles);

            await File.WriteAllTextAsync(paths.DockerResourceLimitsJson, ResultJson.Serialize(new
            {
                targetRequested = result.TargetDockerResourceLimitsRequested,
                targetEffective = result.TargetDockerResourceLimitsEffective,
                loadToolRequested = result.LoadToolDockerResourceLimitsRequested,
                loadToolEffective = result.LoadToolDockerResourceLimitsEffective,
                warnings = result.ResourceLimitWarnings
            }));

            if (result.DockerCleanup is not null)
            {
                await File.WriteAllTextAsync(paths.DockerCleanupJson, ResultJson.Serialize(result.DockerCleanup));
            }

            await File.WriteAllTextAsync(paths.ResultJson, ResultJson.Serialize(result));
            results.Add(result);
            output.WriteLine($"{cell.Implementation.Id}/{cell.Scenario.Id}/{cell.Protocol}: validation={validation.Status}, benchmark={benchmarkExecutionStatus}, loadTool={recordedLoadTool ?? "none"}");
        }

        var runRoot = ArtifactLayout.GetRunRoot(outputDirectory, runId);
        Directory.CreateDirectory(runRoot);
        var generatedAt = DateTimeOffset.UtcNow;
        var report = RunReportBuilder.Build(runId, generatedAt, metadata, results);
        var descriptor = RunReportBuilder.CreateDescriptor(report);

        var runJsonPath = Path.Combine(runRoot, "run.json");
        var aggregateResultsPath = Path.Combine(runRoot, "aggregate-results.json");
        var summaryPath = Path.Combine(runRoot, "summary.md");
        await File.WriteAllTextAsync(runJsonPath, ResultJson.Serialize(descriptor));
        await File.WriteAllTextAsync(aggregateResultsPath, ResultJson.Serialize(report));
        await File.WriteAllTextAsync(summaryPath, MarkdownSummaryWriter.Write(report));

        var evidenceReport = EvidenceReportBuilder.Build(runId, generatedAt, metadata, suiteId: null, suiteTitle: null, cells, compatibilities, results);
        var evidenceReportJsonPath = Path.Combine(runRoot, "evidence-report.json");
        var evidenceReportMdPath = Path.Combine(runRoot, "evidence-report.md");
        await File.WriteAllTextAsync(evidenceReportJsonPath, ResultJson.Serialize(evidenceReport));
        await File.WriteAllTextAsync(evidenceReportMdPath, EvidenceReportMarkdownWriter.Write(evidenceReport));

        return RunnerCommandResult.Create(
            RunnerCommandKind.Run,
            results.Any(result => result.Errors.Count > 0 || result.ValidationResult.Status == ValidationStatus.Failed) ? 1 : 0,
            output.Messages,
            [
                new RunnerArtifactReference("run-descriptor", runJsonPath),
                new RunnerArtifactReference("aggregate-results", aggregateResultsPath),
                new RunnerArtifactReference("summary", summaryPath),
                new RunnerArtifactReference("evidence-report-json", evidenceReportJsonPath),
                new RunnerArtifactReference("evidence-report-md", evidenceReportMdPath)
            ]);
    }

    public RunnerCommandResult Report(string root, RunnerCommandOptions options, IRunnerEventSink? eventSink = null)
    {
        var output = new RunnerOutputBuffer(RunnerCommandKind.Report, eventSink);
        var runId = options.Get("run-id");
        var outputDirectory = options.Get("output") ?? Path.Combine(root, ".artifacts", "runs");

        if (string.IsNullOrWhiteSpace(runId))
        {
            output.WriteError("report requires --run-id.");
            return RunnerCommandResult.Create(RunnerCommandKind.Report, 1, output.Messages);
        }

        var summaryPath = Path.Combine(ArtifactLayout.GetRunRoot(outputDirectory, runId), "summary.md");
        if (!File.Exists(summaryPath))
        {
            output.WriteError($"Summary not found: {summaryPath}");
            return RunnerCommandResult.Create(RunnerCommandKind.Report, 1, output.Messages);
        }

        output.WriteLine(File.ReadAllText(summaryPath));
        return RunnerCommandResult.Create(
            RunnerCommandKind.Report,
            0,
            output.Messages,
            [new RunnerArtifactReference("summary", summaryPath)]);
    }

    private static IReadOnlyList<RunCell> LoadCells(string root, RunnerCommandOptions options)
    {
        return new RunPlanBuilder().Build(root, options);
    }

    private static ScenarioValidationResult BuildCompatibilityValidationResult(RunCell cell, RunCellCompatibility compatibility)
    {
        var reason = compatibility.Reason ?? $"Run cell is {compatibility.Status}.";
        return new ScenarioValidationResult
        {
            ScenarioId = cell.Scenario.Id,
            TargetId = cell.Implementation.Id,
            AdapterId = "",
            Protocol = cell.Protocol,
            Status = ValidationStatus.Unsupported,
            Summary = reason,
            Warnings = [$"{compatibility.Status}: {reason}"]
        };
    }

    private static TargetExecutionResult BuildCompatibilityTargetResult(ArtifactPaths paths, RunCellCompatibility compatibility)
    {
        var reason = compatibility.Reason ?? $"Run cell is {compatibility.Status}.";
        return TargetExecutionResult.UnsupportedResult(reason) with
        {
            TargetExecutionMode = "not-started",
            StdoutPath = paths.TargetStdout,
            StderrPath = paths.TargetStderr,
            LogsPath = paths.CellDirectory,
            Warnings = [$"{compatibility.Status}: {reason}"]
        };
    }

    private static TargetStartOptions BuildTargetStartOptions(RunnerCommandOptions options, string runId)
    {
        var networkMode = options.Get("target-network-mode");
        return new TargetStartOptions(
            options.Get("target-mode"),
            networkMode,
            string.Equals(options.Get("target-docker-build"), "true", StringComparison.OrdinalIgnoreCase),
            options.Get("target-docker-image"),
            string.Equals(networkMode, TargetNetworkModes.SharedDockerNetwork, StringComparison.OrdinalIgnoreCase)
                ? TargetOrchestrator.GenerateDockerNetworkName(runId)
                : null,
            BuildTargetDockerResourceLimits(options),
            options.Get("target-configuration"));
    }

    private static ExecutionProfile BuildExecutionProfile(RunnerCommandOptions options)
    {
        var explicitProfile = options.Get("execution-profile") ?? Environment.GetEnvironmentVariable("PROTOCOL_LAB_EXECUTION_PROFILE");
        if (string.IsNullOrWhiteSpace(explicitProfile) &&
            string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            explicitProfile = "ci-container";
        }

        return ExecutionProfiles.Infer(options.Get("target-mode"), options.Get("target-network-mode"), explicitProfile);
    }

    private static string BuildCellId(RunCell cell)
    {
        return cell.Identity.ToSlug();
    }

    private static DockerResourceLimits? BuildTargetDockerResourceLimits(RunnerCommandOptions options)
    {
        return BuildDockerResourceLimits(
            options.Get("target-cpus"),
            options.Get("target-memory"),
            options.Get("docker-cpuset-cpus"),
            options.Get("docker-memory-swap"),
            ParseLong(options.Get("docker-pids-limit")),
            options.Get("target-memory-reservation"));
    }

    private static DockerResourceLimits? BuildLoadToolDockerResourceLimits(RunnerCommandOptions options)
    {
        return BuildDockerResourceLimits(
            options.Get("load-tool-cpus"),
            options.Get("load-tool-memory"),
            options.Get("docker-cpuset-cpus"),
            options.Get("docker-memory-swap"),
            ParseLong(options.Get("docker-pids-limit")),
            options.Get("load-tool-memory-reservation"));
    }

    private static DockerResourceLimits? BuildDockerResourceLimits(
        string? cpus,
        string? memory,
        string? cpusetCpus,
        string? memorySwap,
        long? pidsLimit,
        string? memoryReservation)
    {
        var limits = new DockerResourceLimits
        {
            Cpus = string.IsNullOrWhiteSpace(cpus) ? null : cpus,
            Memory = string.IsNullOrWhiteSpace(memory) ? null : memory,
            CpusetCpus = string.IsNullOrWhiteSpace(cpusetCpus) ? null : cpusetCpus,
            MemorySwap = string.IsNullOrWhiteSpace(memorySwap) ? null : memorySwap,
            PidsLimit = pidsLimit,
            MemoryReservation = string.IsNullOrWhiteSpace(memoryReservation) ? null : memoryReservation
        };

        return limits.HasAnyLimit ? limits : null;
    }

    private static long? ParseLong(string? value)
    {
        return long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DockerCleanupSummary? MergeDockerCleanup(DockerCleanupSummary? target, DockerCleanupSummary? loadTool)
    {
        if (target is null)
        {
            return loadTool;
        }

        if (loadTool is null)
        {
            return target;
        }

        return target with
        {
            TargetMetricsSamplerCleanupAttempted = target.TargetMetricsSamplerCleanupAttempted || loadTool.TargetMetricsSamplerCleanupAttempted,
            TargetMetricsSamplerCleanupSucceeded = loadTool.TargetMetricsSamplerCleanupSucceeded ?? target.TargetMetricsSamplerCleanupSucceeded,
            LoadToolContainerCleanupAttempted = target.LoadToolContainerCleanupAttempted || loadTool.LoadToolContainerCleanupAttempted,
            LoadToolContainerCleanupSucceeded = loadTool.LoadToolContainerCleanupSucceeded ?? target.LoadToolContainerCleanupSucceeded,
            LoadToolContainerName = loadTool.LoadToolContainerName ?? target.LoadToolContainerName,
            LoadToolMetricsSamplerCleanupAttempted = target.LoadToolMetricsSamplerCleanupAttempted || loadTool.LoadToolMetricsSamplerCleanupAttempted,
            LoadToolMetricsSamplerCleanupSucceeded = loadTool.LoadToolMetricsSamplerCleanupSucceeded ?? target.LoadToolMetricsSamplerCleanupSucceeded,
            LeftoverContainers = target.LeftoverContainers.Concat(loadTool.LeftoverContainers).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            LeftoverNetworks = target.LeftoverNetworks.Concat(loadTool.LeftoverNetworks).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Errors = target.Errors.Concat(loadTool.Errors).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Warnings = target.Warnings.Concat(loadTool.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static IEnumerable<string> FilterTargetDiagnosticWarnings(
        IEnumerable<string> warnings,
        TargetExecutionResult target,
        bool targetDockerMetricsAvailable)
    {
        if (!targetDockerMetricsAvailable ||
            !string.Equals(target.TargetExecutionMode, TargetKinds.Docker, StringComparison.OrdinalIgnoreCase))
        {
            return warnings;
        }

        return warnings.Where(static warning =>
            !warning.StartsWith("Target process metrics were not captured", StringComparison.OrdinalIgnoreCase) &&
            !warning.StartsWith("diagnostic-target-unresolved", StringComparison.OrdinalIgnoreCase) &&
            !warning.StartsWith("diagnostic-target-low-confidence", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string> BuildArtifactMap(ArtifactPaths paths)
    {
        return new Dictionary<string, string>
        {
                ["resultJson"] = paths.ResultJson,
                ["validationJson"] = paths.ValidationJson,
                ["protocolProofJson"] = paths.ProtocolProofJson,
                ["protocolProofStdout"] = paths.ProtocolProofStdout,
                ["protocolProofStderr"] = paths.ProtocolProofStderr,
                ["loadToolStdout"] = paths.LoadToolStdout,
            ["loadToolStderr"] = paths.LoadToolStderr,
            ["h2loadStdout"] = paths.H2loadStdout,
            ["h2loadStderr"] = paths.H2loadStderr,
            ["h2loadOutputJson"] = paths.H2loadOutputJson,
            ["h2loadCommand"] = paths.H2loadCommandTxt,
            ["dockerCommand"] = paths.DockerCommandTxt,
            ["loadToolDockerInspectJson"] = paths.LoadToolDockerInspectJson,
            ["loadToolDockerStatsRaw"] = paths.LoadToolDockerStatsRawTxt,
            ["loadToolDockerStatsJsonl"] = paths.LoadToolDockerStatsJsonl,
            ["loadToolDockerMetricsSummary"] = paths.LoadToolDockerMetricsSummaryJson,
            ["loadToolVersion"] = paths.LoadToolVersion,
            ["loadToolExecution"] = paths.LoadToolExecutionJson,
            ["serverStdout"] = paths.ServerStdout,
            ["serverStderr"] = paths.ServerStderr,
            ["targetStdout"] = paths.TargetStdout,
            ["targetStderr"] = paths.TargetStderr,
            ["targetDockerInspectJson"] = paths.TargetDockerInspectJson,
            ["targetDockerCommand"] = paths.TargetDockerCommandTxt,
            ["targetDockerNetworkInspectJson"] = paths.TargetDockerNetworkInspectJson,
            ["targetDockerStatsRaw"] = paths.TargetDockerStatsRawTxt,
            ["targetDockerStatsJsonl"] = paths.TargetDockerStatsJsonl,
            ["targetDockerMetricsSummary"] = paths.TargetDockerMetricsSummaryJson,
            ["dockerNetworkCommand"] = paths.DockerNetworkCommandTxt,
            ["dockerNetworkInspectJson"] = paths.DockerNetworkInspectJson,
            ["dockerNetworkCleanup"] = paths.DockerNetworkCleanupTxt,
            ["dockerResourceLimits"] = paths.DockerResourceLimitsJson,
            ["dockerCleanup"] = paths.DockerCleanupJson,
            ["diagnosticTarget"] = paths.DiagnosticTargetJson,
            ["adapterHealth"] = paths.AdapterHealthJson,
            ["adapterManifest"] = paths.AdapterManifestJson,
            ["adapterSessionCreate"] = paths.AdapterSessionCreateJson,
            ["adapterPrepare"] = paths.AdapterPrepareJson,
            ["adapterStart"] = paths.AdapterStartJson,
            ["adapterStatusSnapshots"] = paths.AdapterStatusJsonl,
            ["adapterEndpoints"] = paths.AdapterEndpointsJson,
            ["adapterMetrics"] = paths.AdapterMetricsJson,
            ["adapterArtifacts"] = paths.AdapterArtifactsJson,
            ["adapterStop"] = paths.AdapterStopJson,
            ["adapterDelete"] = paths.AdapterDeleteJson,
            ["countersStdout"] = paths.CountersStdout,
            ["countersStderr"] = paths.CountersStderr,
            ["countersRawJson"] = paths.CountersRawJson,
            ["countersRawCsv"] = paths.CountersRawCsv,
            ["countersSummary"] = paths.CountersSummaryJson,
            ["dockerInspectJson"] = paths.DockerInspectJson,
            ["manifestSnapshot"] = paths.ManifestSnapshotJson,
            ["scenarioSnapshot"] = paths.ScenarioSnapshotJson,
            ["targetExecution"] = paths.TargetExecutionJson,
            ["qlog"] = paths.QlogDirectory,
            ["sslkeylog"] = paths.SslKeyLogDirectory,
            ["pcap"] = paths.PcapDirectory,
            ["notes"] = paths.Notes
        };
    }

    private static async Task<ScenarioValidationResult> ValidateCellAsync(string root, RunCell cell, string targetBaseUrl, TargetExecutionResult target, ArtifactPaths paths)
    {
        var networkValidation = ValidateNetworkProfile(root, cell);
        if (networkValidation is not null)
        {
            return networkValidation;
        }

        if (target.Unsupported)
        {
            return new ScenarioValidationResult
            {
                ScenarioId = cell.Scenario.Id,
                TargetId = cell.Implementation.Id,
                AdapterId = "",
                Protocol = cell.Protocol,
                Status = ValidationStatus.Unsupported,
                Summary = target.Errors.FirstOrDefault() ?? "Target startup is unsupported.",
                Warnings = target.Warnings
            };
        }

        if (target.Failed || !target.Ready)
        {
            return new ScenarioValidationResult
            {
                ScenarioId = cell.Scenario.Id,
                TargetId = cell.Implementation.Id,
                AdapterId = "",
                Protocol = cell.Protocol,
                Status = ValidationStatus.Failed,
                Summary = target.Errors.FirstOrDefault() ?? "Target did not become ready.",
                Errors = target.Errors,
                Warnings = target.Warnings
            };
        }

        if (!Uri.TryCreate(targetBaseUrl, UriKind.Absolute, out var uri) ||
            !(string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(uri.Scheme, "quic", StringComparison.OrdinalIgnoreCase)))
        {
            return new ScenarioValidationResult
            {
                ScenarioId = cell.Scenario.Id,
                TargetId = cell.Implementation.Id,
                AdapterId = "",
                Protocol = cell.Protocol,
                Status = ValidationStatus.Unsupported,
                Summary = $"Adapter target returned endpoint '{targetBaseUrl}' which this runner does not yet validate as HTTP/HTTPS or QUIC.",
                Warnings = [BenchmarkEvidenceReasons.AdapterBackedTarget]
            };
        }

        if (string.Equals(uri.Scheme, "quic", StringComparison.OrdinalIgnoreCase))
        {
            return new ScenarioValidationResult
            {
                ScenarioId = cell.Scenario.Id,
                TargetId = cell.Implementation.Id,
                AdapterId = "",
                Protocol = cell.Protocol,
                Status = ValidationStatus.Passed,
                Summary = "Fixture raw QUIC validation passed.",
                Warnings = [BenchmarkEvidenceReasons.AdapterBackedTarget]
            };
        }

        return await HttpScenarioValidator.ValidateAsync(cell, targetBaseUrl, paths, cell.Implementation.CertificateMode);
    }

    private static async Task<(ToolCapability? Capability, string? Error)> ValidateH3LoadToolCapabilityAsync(RunCell cell, ResolvedLoadTool tool)
    {
        if (!ProtocolIds.IsHttp3(cell.Protocol))
        {
            return (null, null);
        }

        if (string.Equals(tool.Manifest.Id, "h2load", StringComparison.OrdinalIgnoreCase))
        {
            var capability = string.Equals(tool.Mode, LoadToolKinds.Docker, StringComparison.OrdinalIgnoreCase)
                ? await LoadToolInvoker.DetectH2loadDockerH3CapabilityAsync(tool.Manifest, pullIfMissing: true)
                : await ProtocolProofValidator.DetectH2loadH3CapabilityAsync();
            return string.Equals(capability.Status, "h3-supported", StringComparison.OrdinalIgnoreCase)
                ? (capability, null)
                : (capability, $"Selected load tool 'h2load' is not H3-capable in this environment: {capability.Status}.{FormatCapabilityWarnings(capability)}");
        }

        if (string.Equals(tool.Manifest.Id, "oha", StringComparison.OrdinalIgnoreCase))
        {
            var capability = await ProtocolProofValidator.DetectOhaH3CapabilityAsync();
            return (capability, $"Selected load tool 'oha' is not accepted for Phase 2G H3 benchmarking: {capability.Status}.{FormatCapabilityWarnings(capability)}");
        }

        if (string.Equals(tool.Manifest.Id, ManagedHttp3LoadGenerator.ToolId, StringComparison.OrdinalIgnoreCase))
        {
            return (new ToolCapability(true, "not-required", Version: RuntimeInformation.FrameworkDescription), null);
        }

        return (null, $"Selected load tool '{tool.Manifest.Id}' has no Phase 2G H3 capability probe.");
    }

    private static string FormatCapabilityWarnings(ToolCapability capability)
    {
        return capability.Warnings is { Count: > 0 }
            ? $" {string.Join(" ", capability.Warnings)}"
            : "";
    }

    private static async Task WriteH2loadProcessOptionProofAsync(string root, RunnerOutputBuffer output)
    {
        var executable = LoadToolInvoker.ResolveExecutable("h2load");
        if (executable is null)
        {
            output.WriteLine("  --output-file proof: no (h2load process executable not found)");
            output.WriteLine("  --qlog-file-base proof: no (h2load process executable not found)");
            return;
        }

        var help = await RunProbeAsync(executable, ["--help"], root);
        var combined = help.Stdout + Environment.NewLine + help.Stderr;
        output.WriteLine($"  --output-file proof: {FormatSupport(combined.Contains("--output-file", StringComparison.OrdinalIgnoreCase))}");
        output.WriteLine($"  --qlog-file-base proof: {FormatSupport(combined.Contains("--qlog-file-base", StringComparison.OrdinalIgnoreCase))}");
    }

    private static void WriteManifestStatus(
        string root,
        string label,
        string directory,
        string fileName,
        List<string> warnings,
        RunnerOutputBuffer output)
    {
        var path = Path.Combine(root, directory, fileName);
        var found = File.Exists(path);
        output.WriteLine($"{label}: {(found ? "found" : "missing")} path={path}");
        if (!found)
        {
            warnings.Add($"{fileName} is missing; restore the repository files before running v1 acceptance.");
        }
    }

    private static void WriteIncursaProjectStatus(string root, List<string> warnings, RunnerOutputBuffer output)
    {
        var manifestPath = Path.Combine(root, "implementations", "incursa-http3.yaml");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            var manifest = YamlFile.Load<ImplementationManifest>(manifestPath);
            var project = manifest.Project;
            if (string.IsNullOrWhiteSpace(project))
            {
                output.WriteLine("Incursa sample project: not declared in manifest");
                warnings.Add("Incursa manifest does not declare a project path.");
                return;
            }

            var projectPath = Path.IsPathFullyQualified(project)
                ? project
                : Path.Combine(root, project);
            var found = File.Exists(projectPath);
            output.WriteLine($"Incursa sample project: {(found ? "found" : "missing")} path={projectPath}");
            if (!found)
            {
                warnings.Add("Incursa endpoint path is missing; point implementations\\incursa-http3.yaml at the repo-owned endpoint project or update the manifest.");
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            output.WriteLine($"Incursa sample project: unknown ({ex.Message})");
            warnings.Add("Incursa manifest could not be parsed.");
        }
    }

    private static void WriteAdapterBackedManifestStatus(string root, List<string> warnings, RunnerOutputBuffer output)
    {
        var manifests = ManifestCatalog.Load(Path.Combine(root, "implementations"));
        var adapterBacked = manifests
            .Where(manifest => string.Equals(manifest.TargetContract, "adapter-v1", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (adapterBacked.Length == 0)
        {
            return;
        }

        output.WriteLine("Adapter-backed implementations:");
        foreach (var manifest in adapterBacked)
        {
            output.WriteLine($"{manifest.Id}\tmode={manifest.TargetKind}\tcontract={manifest.TargetContract}\tbaseUrl={(string.IsNullOrWhiteSpace(manifest.AdapterControlPlaneBaseUrl) ? "n/a" : manifest.AdapterControlPlaneBaseUrl)}");
        }
        warnings.Add("adapter-backed target manifests were detected; ensure any returned endpoint is consumed through the adapter control plane, not the runner boundary.");
    }

    private static void WriteScenarioValidationSummary(string root, List<string> warnings, RunnerOutputBuffer output)
    {
        var fallbackRoot = root;
        if (!Directory.Exists(Path.Combine(root, "scenarios")))
        {
            output.WriteLine("Scenario validation: skipped (scenarios directory not found)");
            return;
        }

        output.WriteLine("Scenario catalog validation:");
        var scenarios = ScenarioCatalog.Load(Path.Combine(root, "scenarios"));
        output.WriteLine($"  scenarios loaded: {scenarios.Count}");

        var stable = scenarios.Count(s => s.IsStable());
        var experimental = scenarios.Count(s => s.IsExperimental());
        var placeholder = scenarios.Count(s => s.IsPlaceholder());
        output.WriteLine($"  by status: stable={stable}, experimental={experimental}, placeholder={placeholder}");

        var invalidCount = 0;
        foreach (var scenario in scenarios)
        {
            var errors = ScenarioValidator.Validate(scenario);
            if (errors.Count > 0)
            {
                invalidCount++;
                output.WriteLine($"  INVALID: {scenario.Id}");
                foreach (var error in errors)
                {
                    output.WriteLine($"    - {error}");
                }

                warnings.Add($"Scenario '{scenario.Id}' failed validation and should be fixed.");
            }
        }

        if (invalidCount == 0)
        {
            output.WriteLine("  all scenarios passed structural validation");
        }
        else
        {
            output.WriteLine($"  {invalidCount} scenario(s) failed validation");
            warnings.Add("One or more scenario files failed validation and should be fixed before marking acceptance as ready.");
        }
    }

    private static async Task WriteDockerTargetImageStatusAsync(
        string root,
        string label,
        string directory,
        string fileName,
        string buildScript,
        List<string> warnings,
        RunnerOutputBuffer output)
    {
        var path = Path.Combine(root, directory, fileName);
        if (!File.Exists(path))
        {
            return;
        }

        var docker = LoadToolInvoker.ResolveExecutable("docker");
        if (docker is null)
        {
            output.WriteLine($"{label}: unknown (Docker unavailable)");
            warnings.Add("Docker target mode requires Docker Desktop. Install/start Docker Desktop before Docker target acceptance.");
            return;
        }

        try
        {
            var manifest = YamlFile.Load<ImplementationManifest>(path);
            if (string.IsNullOrWhiteSpace(manifest.Image))
            {
                output.WriteLine($"{label}: not declared");
                return;
            }

            var inspect = await RunProbeAsync(docker, ["image", "inspect", manifest.Image], root);
            output.WriteLine($"{label}: image={manifest.Image} available={(inspect.ExitCode == 0 ? "yes" : "no")} build={buildScript}");
            if (inspect.ExitCode != 0)
            {
                warnings.Add($"Build the {label.ToLowerInvariant()} with {buildScript} before Docker target acceptance.");
            }

            if (inspect.ExitCode == 0 &&
                manifest.TargetCapabilityProof is { DockerExecArguments.Count: > 0 } proof)
            {
                var proofArguments = new List<string>
                {
                    "run",
                    "--rm",
                    "--entrypoint",
                    proof.DockerExecArguments[0],
                    manifest.Image
                };
                proofArguments.AddRange(proof.DockerExecArguments.Skip(1));
                var proofResult = await RunProbeAsync(docker, proofArguments, root);
                var proofOutput = proofResult.Stdout + proofResult.Stderr;
                var proofPassed = proofResult.ExitCode == 0 &&
                    (string.IsNullOrWhiteSpace(proof.ExpectedOutputContains) ||
                     proofOutput.Contains(proof.ExpectedOutputContains, StringComparison.OrdinalIgnoreCase));
                output.WriteLine($"  target capability proof: id={proof.Id} status={(proofPassed ? "passed" : "failed")} expected={proof.ExpectedOutputContains}");
                if (!proofPassed)
                {
                    warnings.Add($"Target capability proof '{proof.Id}' failed for {manifest.Image}. Rebuild with {buildScript} or inspect nginx -V output.");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            output.WriteLine($"{label}: unknown ({ex.Message})");
            warnings.Add($"{fileName} could not be parsed for Docker target image status.");
        }
    }

    private static bool HasWarning(ToolCapability capability, string text)
    {
        return capability.Warnings?.Any(warning => warning.Contains(text, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static string FormatSupport(bool supported)
    {
        return supported ? "yes" : "no";
    }

    private static async Task<ProcessProbe> RunProbeAsync(string executable, IReadOnlyList<string> arguments, string workingDirectory)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = executable,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new ProcessProbe(process.ExitCode, stdout, stderr);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ProcessProbe(1, "", ex.Message);
        }
    }

    private static void PrepareCellDirectories(ArtifactPaths paths)
    {
        Directory.CreateDirectory(paths.CellDirectory);
        Directory.CreateDirectory(paths.QlogDirectory);
        Directory.CreateDirectory(paths.SslKeyLogDirectory);
        Directory.CreateDirectory(paths.PcapDirectory);
        File.WriteAllText(paths.LoadToolStdout, "");
        File.WriteAllText(paths.LoadToolStderr, "");
        File.WriteAllText(paths.TargetStdout, "");
        File.WriteAllText(paths.TargetStderr, "");
        File.WriteAllText(paths.ServerStdout, "");
        File.WriteAllText(paths.ServerStderr, "");
        File.WriteAllText(paths.LoadToolVersion, "");
        File.WriteAllText(paths.H2loadOutputJson, "");
        File.WriteAllText(paths.H2loadCommandTxt, "");
        File.WriteAllText(paths.DockerCommandTxt, "");
        File.WriteAllText(paths.LoadToolDockerStatsRawTxt, "");
        File.WriteAllText(paths.LoadToolDockerStatsJsonl, "");
        File.WriteAllText(paths.LoadToolDockerMetricsSummaryJson, "");
        File.WriteAllText(paths.TargetDockerCommandTxt, "");
        File.WriteAllText(paths.TargetDockerInspectJson, "");
        File.WriteAllText(paths.TargetDockerStatsRawTxt, "");
        File.WriteAllText(paths.TargetDockerStatsJsonl, "");
        File.WriteAllText(paths.TargetDockerMetricsSummaryJson, "");
        File.WriteAllText(paths.CountersStdout, "");
        File.WriteAllText(paths.CountersStderr, "");
        File.WriteAllText(paths.CountersRawJson, "");
        File.WriteAllText(paths.CountersRawCsv, "");
        File.WriteAllText(paths.CountersSummaryJson, "");
        File.WriteAllText(paths.DiagnosticTargetJson, "");
        File.WriteAllText(paths.AdapterHealthJson, "");
        File.WriteAllText(paths.AdapterManifestJson, "");
        File.WriteAllText(paths.AdapterSessionCreateJson, "");
        File.WriteAllText(paths.AdapterPrepareJson, "");
        File.WriteAllText(paths.AdapterStartJson, "");
        File.WriteAllText(paths.AdapterStatusJsonl, "");
        File.WriteAllText(paths.AdapterEndpointsJson, "");
        File.WriteAllText(paths.AdapterMetricsJson, "");
        File.WriteAllText(paths.AdapterArtifactsJson, "");
        File.WriteAllText(paths.AdapterStopJson, "");
        File.WriteAllText(paths.AdapterDeleteJson, "");
    }

    private static async Task WriteSnapshotsAsync(ArtifactPaths paths, RunCell cell)
    {
        await File.WriteAllTextAsync(paths.ManifestSnapshotJson, ResultJson.Serialize(cell.Implementation));
        await File.WriteAllTextAsync(paths.ScenarioSnapshotJson, ResultJson.Serialize(cell.Scenario));
    }

    private static async Task WriteNotesAsync(ArtifactPaths paths, IReadOnlyList<string> warnings, IReadOnlyList<string> errors)
    {
        if (warnings.Count == 0 && errors.Count == 0)
        {
            return;
        }

        var lines = new List<string>();
        if (warnings.Count > 0)
        {
            lines.Add("Warnings:");
            lines.AddRange(warnings.Select(warning => "- " + warning));
        }

        if (errors.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add("");
            }

            lines.Add("Errors:");
            lines.AddRange(errors.Select(error => "- " + error));
        }

        await File.WriteAllLinesAsync(paths.Notes, lines);
    }

    private static int CountFiles(string directory)
    {
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count()
            : 0;
    }

    private static RequestedLoadShape BuildRequestedLoadShape(RunCell cell)
    {
        return new RequestedLoadShape
        {
            Connections = cell.Connections,
            Concurrency = cell.Connections,
            StreamsPerConnection = cell.StreamsPerConnection,
            DurationSeconds = cell.DurationSeconds,
            WarmupSeconds = cell.WarmupSeconds,
            Repetitions = cell.Repetition
        };
    }

    private static EffectiveLoadShape BuildSkippedEffectiveLoadShape(RunCell cell)
    {
        var warnings = new List<string>();
        if (ProtocolIds.IsHttp1(cell.Protocol) && cell.StreamsPerConnection != 1)
        {
            warnings.Add("HTTP/1.1 does not support streamsPerConnection; the requested value is not applicable.");
        }

        return new EffectiveLoadShape
        {
            Connections = cell.Connections,
            Concurrency = cell.Connections,
            StreamsPerConnection = ProtocolIds.IsHttp1(cell.Protocol) ? 1 : cell.StreamsPerConnection,
            DurationSeconds = cell.DurationSeconds,
            WarmupSeconds = cell.WarmupSeconds,
            Repetitions = cell.Repetition,
            Notes = ["No load-tool execution completed for this cell."],
            Warnings = warnings
        };
    }

    private static string GetRepetitionGroupKey(RunCell cell)
    {
        return string.Join(
            "\u001f",
            cell.Implementation.Id,
            cell.Scenario.Id,
            cell.Protocol,
            ExecutionProfiles.ToId(cell.ExecutionProfile),
            string.IsNullOrWhiteSpace(cell.LoadProfileId) ? "no-load-profile" : cell.LoadProfileId,
            cell.NetworkProfile,
            cell.Connections.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cell.StreamsPerConnection.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cell.DurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cell.WarmupSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static IReadOnlyList<string> BuildFairnessWarnings(
        RunCell cell,
        string baseUrl,
        int totalRepetitions,
        bool? parsedMetricsAvailable)
    {
        var warnings = new List<string>();
        if (ProtocolIds.IsHttp1(cell.Protocol) && cell.StreamsPerConnection != 1)
        {
            warnings.Add("HTTP/1.1 does not support streamsPerConnection; treat the effective stream count as 1.");
        }

        if (ProtocolIds.IsHttp3(cell.Protocol))
        {
            warnings.Add("HTTP/3 cells require explicit protocol proof; fallback is not accepted as validation success.");
        }
        else
        {
            warnings.Add($"Validation proves endpoint behavior for protocol selection '{cell.Protocol}', but it does not independently prove negotiated wire protocol.");
        }

        if (totalRepetitions == 1)
        {
            warnings.Add("Only one repetition was run; treat this as a single-run sample, not a stable benchmark.");
        }

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
            (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("Target URL is localhost; client and server share host resources.");
            warnings.Add("Server and load tool run in the same local environment; publishable comparisons need isolated resource controls.");
        }

        if (parsedMetricsAvailable == false)
        {
            warnings.Add("Benchmark completed but parsed metrics are unavailable; inspect raw load-tool stdout/stderr.");
        }

        return warnings;
    }

    private static string? FirstLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
    }

    private static ScenarioValidationResult? ValidateNetworkProfile(string root, RunCell cell)
    {
        var profiles = NetworkProfileCatalog.Load(Path.Combine(root, "scenarios", "network", "profiles"));
        var profile = profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, cell.NetworkProfile, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
        {
            return new ScenarioValidationResult
            {
                ScenarioId = cell.Scenario.Id,
                TargetId = cell.Implementation.Id,
                AdapterId = "",
                Protocol = cell.Protocol,
                Status = ValidationStatus.Failed,
                Summary = $"Network profile '{cell.NetworkProfile}' was not found.",
                Errors = [$"Network profile '{cell.NetworkProfile}' was not found."]
            };
        }

        var support = NetworkProfileSupportEvaluator.Evaluate(profile);
        return support.IsSupported
            ? null
            : new ScenarioValidationResult
            {
                ScenarioId = cell.Scenario.Id,
                TargetId = cell.Implementation.Id,
                AdapterId = "",
                Protocol = cell.Protocol,
                Status = ValidationStatus.Unsupported,
                Summary = support.Reason
            };
    }

    private static IReadOnlyCollection<string>? SplitCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static IReadOnlyCollection<int>? SplitIntCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static BenchmarkResult PopulateLoadProfileMetadata(BenchmarkResult result, RunCell cell, IReadOnlyList<LoadProfileDefinition>? profiles)
    {
        if (string.IsNullOrWhiteSpace(cell.LoadProfileId) || profiles is null || profiles.Count == 0)
        {
            return result;
        }

        var profile = profiles.FirstOrDefault(p =>
            string.Equals(p.Id, cell.LoadProfileId, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return result;
        }

        return result with
        {
            LoadProfileTitle = profile.Title,
            LoadProfilePurpose = profile.Purpose
        };
    }

}

