// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using NJsonSchema;
using YamlDotNet.Serialization;

namespace Incursa.ProtocolLab.Adapter.Conformance;

public sealed class RunPlanConformanceValidator
{
    public async Task<RunPlanConformanceReport> ValidateAsync(
        string runPlanPath,
        RunPlanConformanceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runPlanPath);
        ArgumentNullException.ThrowIfNull(options);

        var steps = new List<RunPlanConformanceStepResult>();
        var overall = RunPlanConformanceOutcome.Passed;
        string? runPlanId = null;
        string? runPlanVersion = null;

        void Add(RunPlanConformanceStepResult step)
        {
            steps.Add(step);
            if (step.Outcome == RunPlanConformanceOutcome.Failed)
            {
                overall = RunPlanConformanceOutcome.Failed;
            }
        }

        if (string.IsNullOrWhiteSpace(options.RunPlanSchemaPath) || !File.Exists(options.RunPlanSchemaPath))
        {
            Add(Failed("run-plan-schema", "Run plan v1 schema file was not found.", options.RunPlanSchemaPath));
            return new RunPlanConformanceReport(runPlanPath, overall, steps);
        }

        if (!File.Exists(runPlanPath))
        {
            Add(Failed("run-plan-open", "Run plan path was not found.", runPlanPath));
            return new RunPlanConformanceReport(runPlanPath, overall, steps);
        }

        var runPlanJson = await File.ReadAllTextAsync(runPlanPath, cancellationToken).ConfigureAwait(false);
        JsonDocument runPlanDocument;
        try
        {
            runPlanDocument = JsonDocument.Parse(runPlanJson);
        }
        catch (JsonException ex)
        {
            Add(Failed("run-plan-json", $"Run plan is malformed JSON: {ex.Message}", runPlanPath));
            return new RunPlanConformanceReport(runPlanPath, overall, steps);
        }

        using (runPlanDocument)
        {
            var runPlanSchema = await ContractSchemaLoader.LoadAsync(options.RunPlanSchemaPath, cancellationToken).ConfigureAwait(false);
            var schemaErrors = runPlanSchema.Validate(runPlanJson);
            if (schemaErrors.Count == 0)
            {
                Add(Passed("run-plan-v1-schema", "Run plan passed v1 schema validation.", options.RunPlanSchemaPath));
            }
            else
            {
                Add(Failed(
                    "run-plan-v1-schema",
                    "Run plan failed v1 schema validation.",
                    options.RunPlanSchemaPath,
                    schemaErrors.Select(static error => error.ToString()).ToArray()));
            }

            var root = runPlanDocument.RootElement;
            runPlanId = ReadString(root, "runPlanId");
            runPlanVersion = ReadString(root, "runPlanVersion");

            var packages = await LoadPackagesAsync(options, Add, cancellationToken).ConfigureAwait(false);
            ValidatePackageReferences(root, packages, Add);
            ValidateSelections(root, packages, Add);
        }

        return new RunPlanConformanceReport(runPlanPath, overall, steps, runPlanId, runPlanVersion);
    }

    private static async Task<IReadOnlyList<PackageSnapshot>> LoadPackagesAsync(
        RunPlanConformanceOptions options,
        Action<RunPlanConformanceStepResult> add,
        CancellationToken cancellationToken)
    {
        var packages = new List<PackageSnapshot>();
        var packageValidator = new PackageConformanceValidator();
        foreach (var packagePath in options.PackagePaths)
        {
            if (options.ValidatePackages)
            {
                var packageReport = await packageValidator.ValidateAsync(
                    packagePath,
                    new PackageConformanceOptions
                    {
                        PackageSchemaPath = options.PackageSchemaPath,
                        TestExecutorSchemaRootPath = options.TestExecutorSchemaRootPath,
                        ScenarioSchemaPath = options.ScenarioSchemaPath
                    },
                    cancellationToken).ConfigureAwait(false);

                if (packageReport.Outcome == PackageConformanceOutcome.Passed)
                {
                    add(Passed("package-conformance", "Referenced package passed package conformance.", packagePath));
                }
                else
                {
                    add(Failed(
                        "package-conformance",
                        "Referenced package failed package conformance.",
                        packagePath,
                        packageReport.Steps
                            .Where(static step => step.Outcome == PackageConformanceOutcome.Failed)
                            .Select(static step => $"{step.Step}: {step.Message}")
                            .ToArray()));
                }
            }

            if (!TryLoadPackage(packagePath, out var package, out var diagnostic))
            {
                add(Failed("package-load", diagnostic, packagePath));
                continue;
            }

            packages.Add(package);
        }

        if (packages.Count > 0)
        {
            add(Passed("package-set", $"Loaded {packages.Count} package manifest(s) for run-plan selector validation.", null));
        }
        else
        {
            add(Failed("package-set", "Run-plan conformance requires at least one resolved package manifest.", null));
        }

        return packages;
    }

    private static void ValidatePackageReferences(
        JsonElement runPlan,
        IReadOnlyList<PackageSnapshot> packages,
        Action<RunPlanConformanceStepResult> add)
    {
        var diagnostics = new List<string>();
        var references = ReadPackageReferences(runPlan).ToArray();
        foreach (var reference in references)
        {
            var package = packages.FirstOrDefault(candidate =>
                string.Equals(candidate.PackageId, reference.PackageId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.PackageVersion, reference.PackageVersion, StringComparison.OrdinalIgnoreCase));
            if (package is null)
            {
                diagnostics.Add($"Package reference {reference.PackageId}:{reference.PackageVersion} was not supplied to the validator.");
                continue;
            }

            if (package.Sha256 is not null &&
                !string.Equals(package.Sha256, reference.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add($"Package reference {reference.PackageId}:{reference.PackageVersion} SHA-256 mismatch: run plan {reference.Sha256}, package {package.Sha256}.");
            }
        }

        foreach (var package in packages)
        {
            if (!references.Any(reference =>
                    string.Equals(reference.PackageId, package.PackageId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(reference.PackageVersion, package.PackageVersion, StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add($"Resolved package {package.PackageId}:{package.PackageVersion} is not pinned by the run plan.");
            }
        }

        if (diagnostics.Count == 0)
        {
            add(Passed("run-plan-package-references", "Run plan package references resolve to the supplied package set.", "packages"));
            return;
        }

        add(Failed("run-plan-package-references", "Run plan package references must match the supplied package set.", "packages", diagnostics));
    }

    private static void ValidateSelections(
        JsonElement runPlan,
        IReadOnlyList<PackageSnapshot> packages,
        Action<RunPlanConformanceStepResult> add)
    {
        var diagnostics = new List<string>();
        var selectedImplementations = ReadStringArray(runPlan, "implementationIds");
        var selectedExecutors = ReadStringArray(runPlan, "testExecutorIds");
        var selectedSuites = ReadStringArray(runPlan, "suiteIds");
        var selectedScenarios = ReadStringArray(runPlan, "scenarioIds").ToList();
        var selectedProtocols = ReadStringArray(runPlan, "protocols");

        var scenarioById = packages
            .SelectMany(static package => package.Scenarios)
            .GroupBy(static scenario => scenario.ScenarioId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        var suiteById = packages
            .SelectMany(static package => package.Suites)
            .GroupBy(static suite => suite.SuiteId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        var implementationById = packages
            .SelectMany(static package => package.Implementations)
            .GroupBy(static implementation => implementation.ImplementationId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        var executorById = packages
            .SelectMany(static package => package.TestExecutors)
            .GroupBy(static executor => executor.TestExecutorId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var suiteId in selectedSuites)
        {
            if (!suiteById.TryGetValue(suiteId, out var suite))
            {
                diagnostics.Add($"Selected suite '{suiteId}' is not provided by the supplied packages.");
                continue;
            }

            foreach (var scenarioId in suite.ScenarioIds)
            {
                if (!selectedScenarios.Contains(scenarioId, StringComparer.OrdinalIgnoreCase))
                {
                    selectedScenarios.Add(scenarioId);
                }
            }
        }

        foreach (var scenarioId in selectedScenarios)
        {
            if (!scenarioById.TryGetValue(scenarioId, out var scenario))
            {
                diagnostics.Add($"Selected scenario '{scenarioId}' is not provided by the supplied scenario packages.");
                continue;
            }

            if (!selectedProtocols.Any(protocol => scenario.Protocols.Contains(protocol, StringComparer.OrdinalIgnoreCase)))
            {
                diagnostics.Add($"Selected scenario '{scenarioId}' protocols [{string.Join(", ", scenario.Protocols)}] do not match run-plan protocols [{string.Join(", ", selectedProtocols)}].");
            }
        }

        foreach (var implementationId in selectedImplementations)
        {
            if (!implementationById.TryGetValue(implementationId, out var implementation))
            {
                diagnostics.Add($"Selected implementation '{implementationId}' is not provided by the supplied implementation packages.");
                continue;
            }

            ValidateProviderCoversSelection("implementation", implementationId, implementation.Protocols, implementation.Scenarios, selectedProtocols, selectedScenarios, diagnostics);
        }

        foreach (var executorId in selectedExecutors)
        {
            if (!executorById.TryGetValue(executorId, out var executor))
            {
                diagnostics.Add($"Selected test executor '{executorId}' is not provided by the supplied test-executor packages.");
                continue;
            }

            ValidateProviderCoversSelection("test executor", executorId, executor.Protocols, executor.Scenarios, selectedProtocols, selectedScenarios, diagnostics);
            if (executor.Tests.Count > 0)
            {
                foreach (var scenarioId in selectedScenarios)
                {
                    if (!executor.Tests.Contains(scenarioId, StringComparer.OrdinalIgnoreCase))
                    {
                        diagnostics.Add($"Selected test executor '{executorId}' tests do not include selected test/scenario id '{scenarioId}'.");
                    }
                }
            }
        }

        foreach (var suiteId in selectedSuites)
        {
            if (!suiteById.TryGetValue(suiteId, out var suite))
            {
                continue;
            }

            foreach (var executorId in suite.TestExecutors)
            {
                if (selectedExecutors.Count > 0 && !selectedExecutors.Contains(executorId, StringComparer.OrdinalIgnoreCase))
                {
                    diagnostics.Add($"Selected suite '{suiteId}' requires test executor '{executorId}', but the run plan selected [{string.Join(", ", selectedExecutors)}].");
                }
            }
        }

        if (diagnostics.Count == 0)
        {
            add(Passed("run-plan-selector-compatibility", "Run plan selectors resolve to compatible package-provided implementations, executors, suites, scenarios, and protocols.", null));
            return;
        }

        add(Failed("run-plan-selector-compatibility", "Run plan selectors must resolve to compatible package-provided components before job creation.", null, diagnostics));
    }

    private static void ValidateProviderCoversSelection(
        string providerKind,
        string providerId,
        IReadOnlyList<string> providerProtocols,
        IReadOnlyList<string> providerScenarios,
        IReadOnlyList<string> selectedProtocols,
        IReadOnlyList<string> selectedScenarios,
        ICollection<string> diagnostics)
    {
        foreach (var protocol in selectedProtocols)
        {
            if (!providerProtocols.Contains(protocol, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add($"Selected {providerKind} '{providerId}' does not declare protocol '{protocol}'.");
            }
        }

        foreach (var scenarioId in selectedScenarios)
        {
            if (!providerScenarios.Contains(scenarioId, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add($"Selected {providerKind} '{providerId}' does not declare scenario/test compatibility for '{scenarioId}'.");
            }
        }
    }

    private static bool TryLoadPackage(string packagePath, out PackageSnapshot package, out string diagnostic)
    {
        package = default!;
        diagnostic = "";
        using var reader = PackageContentReader.Open(packagePath);
        if (!reader.Exists)
        {
            diagnostic = "Package path must be a directory or .plabpkg archive.";
            return false;
        }

        if (!reader.TryReadText("protocol-lab-package.json", out var manifestJson))
        {
            diagnostic = "Package root must contain protocol-lab-package.json.";
            return false;
        }

        using var document = JsonDocument.Parse(manifestJson);
        var root = document.RootElement;
        var packageId = ReadString(root, "packageId") ?? "";
        var packageVersion = ReadString(root, "packageVersion") ?? "";
        var kind = ReadString(root, "kind") ?? "";
        package = new PackageSnapshot(
            packageId,
            packageVersion,
            kind,
            reader.Sha256,
            ReadImplementations(root),
            ReadTestExecutors(root),
            ReadScenarios(root),
            ReadSuites(root, reader));
        return true;
    }

    private static IReadOnlyList<ProvidedImplementation> ReadImplementations(JsonElement root)
    {
        if (!root.TryGetProperty("providedImplementations", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(static item => new ProvidedImplementation(
                ReadString(item, "implementationId") ?? "",
                ReadStringArray(item, "protocols"),
                ReadStringArray(item, "scenarios")))
            .ToArray();
    }

    private static IReadOnlyList<ProvidedTestExecutor> ReadTestExecutors(JsonElement root)
    {
        if (!root.TryGetProperty("providedTestExecutors", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(static item => new ProvidedTestExecutor(
                ReadString(item, "testExecutorId") ?? "",
                ReadStringArray(item, "protocols"),
                ReadStringArray(item, "scenarios"),
                ReadStringArray(item, "tests")))
            .ToArray();
    }

    private static IReadOnlyList<ProvidedScenario> ReadScenarios(JsonElement root)
    {
        if (!root.TryGetProperty("providedScenarios", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(static item => new ProvidedScenario(
                ReadString(item, "scenarioId") ?? "",
                ReadStringArray(item, "protocols")))
            .ToArray();
    }

    private static IReadOnlyList<ProvidedSuite> ReadSuites(JsonElement root, PackageContentReader reader)
    {
        if (!root.TryGetProperty("providedSuites", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(item =>
            {
                var suiteId = ReadString(item, "suiteId") ?? "";
                return new ProvidedSuite(
                    suiteId,
                    ReadStringArray(item, "protocols"),
                    ReadStringArray(item, "testExecutors"),
                    TryReadSuiteScenarioIds(reader, suiteId, out var scenarios) ? scenarios : []);
            })
            .ToArray();
    }

    private static bool TryReadSuiteScenarioIds(PackageContentReader reader, string suiteId, out IReadOnlyList<string> scenarios)
    {
        scenarios = [];
        if (string.IsNullOrWhiteSpace(suiteId) || !reader.TryReadText($"suites/{suiteId}.yaml", out var suiteYaml))
        {
            return false;
        }

        var json = ConvertDocumentToJson($"suites/{suiteId}.yaml", suiteYaml);
        using var document = JsonDocument.Parse(json);
        scenarios = ReadStringArray(document.RootElement, "scenarios");
        return scenarios.Count > 0;
    }

    private static IEnumerable<PackageReference> ReadPackageReferences(JsonElement root)
    {
        if (!root.TryGetProperty("packages", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in array.EnumerateArray())
        {
            yield return new PackageReference(
                ReadString(item, "packageId") ?? "",
                ReadString(item, "packageVersion") ?? "",
                ReadString(item, "sha256") ?? "");
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString() ?? "")
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string ConvertDocumentToJson(string path, string content)
    {
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return content;
        }

        var deserializer = new DeserializerBuilder()
            .WithAttemptingUnquotedStringTypeDeserialization()
            .Build();
        var serializer = new SerializerBuilder().JsonCompatible().Build();
        var yamlObject = deserializer.Deserialize(new StringReader(content));
        return serializer.Serialize(yamlObject);
    }

    private static RunPlanConformanceStepResult Passed(string step, string message, string? path = null)
    {
        return new RunPlanConformanceStepResult(step, RunPlanConformanceOutcome.Passed, message, path);
    }

    private static RunPlanConformanceStepResult Failed(
        string step,
        string message,
        string? path = null,
        IReadOnlyList<string>? diagnostics = null)
    {
        return new RunPlanConformanceStepResult(step, RunPlanConformanceOutcome.Failed, message, path, diagnostics);
    }

    private sealed record PackageReference(string PackageId, string PackageVersion, string Sha256);

    private sealed record PackageSnapshot(
        string PackageId,
        string PackageVersion,
        string Kind,
        string? Sha256,
        IReadOnlyList<ProvidedImplementation> Implementations,
        IReadOnlyList<ProvidedTestExecutor> TestExecutors,
        IReadOnlyList<ProvidedScenario> Scenarios,
        IReadOnlyList<ProvidedSuite> Suites);

    private sealed record ProvidedImplementation(string ImplementationId, IReadOnlyList<string> Protocols, IReadOnlyList<string> Scenarios);

    private sealed record ProvidedTestExecutor(string TestExecutorId, IReadOnlyList<string> Protocols, IReadOnlyList<string> Scenarios, IReadOnlyList<string> Tests);

    private sealed record ProvidedScenario(string ScenarioId, IReadOnlyList<string> Protocols);

    private sealed record ProvidedSuite(string SuiteId, IReadOnlyList<string> Protocols, IReadOnlyList<string> TestExecutors, IReadOnlyList<string> ScenarioIds);

    private sealed class PackageContentReader : IDisposable
    {
        private readonly string packagePath;
        private readonly ZipArchive? archive;

        private PackageContentReader(string packagePath, ZipArchive? archive, bool exists, string? sha256)
        {
            this.packagePath = packagePath;
            this.archive = archive;
            Exists = exists;
            Sha256 = sha256;
        }

        public bool Exists { get; }

        public string? Sha256 { get; }

        public static PackageContentReader Open(string packagePath)
        {
            if (Directory.Exists(packagePath))
            {
                return new PackageContentReader(Path.GetFullPath(packagePath), null, exists: true, sha256: null);
            }

            if (!File.Exists(packagePath))
            {
                return new PackageContentReader(packagePath, null, exists: false, sha256: null);
            }

            try
            {
                return new PackageContentReader(packagePath, ZipFile.OpenRead(packagePath), exists: true, sha256: ComputeSha256(packagePath));
            }
            catch (InvalidDataException)
            {
                return new PackageContentReader(packagePath, null, exists: false, sha256: null);
            }
        }

        public bool TryReadText(string relativePath, out string content)
        {
            content = "";
            if (!IsPackageRelativePath(relativePath))
            {
                return false;
            }

            if (archive is null)
            {
                var fullPath = Path.GetFullPath(Path.Combine(packagePath, relativePath));
                var rootWithSeparator = packagePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(fullPath))
                {
                    return false;
                }

                content = File.ReadAllText(fullPath);
                return true;
            }

            var entry = archive.GetEntry(relativePath.Replace('\\', '/'));
            if (entry is null)
            {
                return false;
            }

            using var reader = new StreamReader(entry.Open());
            content = reader.ReadToEnd();
            return true;
        }

        public void Dispose()
        {
            archive?.Dispose();
        }

        private static bool IsPackageRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                Path.IsPathRooted(path) ||
                path.Contains(':', StringComparison.Ordinal))
            {
                return false;
            }

            var segments = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
            return !segments.Any(static segment => segment == "..");
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
