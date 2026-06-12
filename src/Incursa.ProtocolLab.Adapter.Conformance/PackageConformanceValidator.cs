// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using NJsonSchema;
using YamlDotNet.Serialization;

namespace Incursa.ProtocolLab.Adapter.Conformance;

public sealed class PackageConformanceValidator
{
    public async Task<PackageConformanceReport> ValidateAsync(
        string packagePath,
        PackageConformanceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(options);

        var steps = new List<PackageConformanceStepResult>();
        var overall = PackageConformanceOutcome.Passed;
        string? packageId = null;
        string? packageVersion = null;
        string? kind = null;

        void Add(PackageConformanceStepResult step)
        {
            steps.Add(step);
            if (step.Outcome == PackageConformanceOutcome.Failed)
            {
                overall = PackageConformanceOutcome.Failed;
            }
        }

        if (string.IsNullOrWhiteSpace(options.PackageSchemaPath) || !File.Exists(options.PackageSchemaPath))
        {
            Add(Failed("package-schema", "Package v2 schema file was not found.", options.PackageSchemaPath));
            return new PackageConformanceReport(packagePath, overall, steps);
        }

        using var package = PackageContentReader.Open(packagePath);
        if (!package.Exists)
        {
            Add(Failed("package-open", "Package path must be a directory or .plabpkg archive.", packagePath));
            return new PackageConformanceReport(packagePath, overall, steps);
        }

        if (!package.TryReadText("protocol-lab-package.json", out var rootManifest))
        {
            Add(Failed("package-root", "Package root must contain protocol-lab-package.json.", "protocol-lab-package.json"));
            return new PackageConformanceReport(packagePath, overall, steps);
        }

        Add(Passed("package-root", "Found package root manifest.", "protocol-lab-package.json"));

        JsonDocument rootDocument;
        try
        {
            rootDocument = JsonDocument.Parse(rootManifest);
        }
        catch (JsonException ex)
        {
            Add(Failed("package-root-json", $"protocol-lab-package.json is malformed JSON: {ex.Message}", "protocol-lab-package.json"));
            return new PackageConformanceReport(packagePath, overall, steps);
        }

        using (rootDocument)
        {
            var packageSchema = await ContractSchemaLoader.LoadAsync(options.PackageSchemaPath, cancellationToken).ConfigureAwait(false);
            var schemaErrors = packageSchema.Validate(rootManifest);
            if (schemaErrors.Count == 0)
            {
                Add(Passed("package-v2-schema", "protocol-lab-package.json passed package v2 schema validation.", options.PackageSchemaPath));
            }
            else
            {
                Add(Failed(
                    "package-v2-schema",
                    "protocol-lab-package.json failed package v2 schema validation.",
                    options.PackageSchemaPath,
                    schemaErrors.Select(static error => error.ToString()).ToArray()));
            }

            var root = rootDocument.RootElement;
            packageId = ReadString(root, "packageId");
            packageVersion = ReadString(root, "packageVersion");
            kind = ReadString(root, "kind");

            var entryManifests = ReadStringArray(root, "entryManifests");
            if (entryManifests.Count == 0 && !string.Equals(kind, "toolchain", StringComparison.OrdinalIgnoreCase))
            {
                Add(Failed("entry-manifests", "Runtime component packages must declare at least one entry manifest.", "entryManifests"));
            }

            foreach (var entryManifest in entryManifests)
            {
                ValidateEntryPath(package, entryManifest, Add);
            }

            if (options.ValidateEntryManifestSchemas)
            {
                await ValidateEntrySchemasAsync(package, root, kind, entryManifests, options, Add, cancellationToken).ConfigureAwait(false);
            }
        }

        return new PackageConformanceReport(packagePath, overall, steps, packageId, packageVersion, kind);
    }

    private static void ValidateEntryPath(
        PackageContentReader package,
        string entryManifest,
        Action<PackageConformanceStepResult> add)
    {
        if (!IsPackageRelativePath(entryManifest))
        {
            add(Failed("entry-manifest-path", "Entry manifest path must be package-relative and must not contain traversal, rooted paths, or URI prefixes.", entryManifest));
            return;
        }

        if (!package.TryReadText(entryManifest, out _))
        {
            add(Failed("entry-manifest-exists", "Entry manifest file was not found in the package.", entryManifest));
            return;
        }

        add(Passed("entry-manifest-exists", "Entry manifest file exists.", entryManifest));
    }

    private static async Task ValidateEntrySchemasAsync(
        PackageContentReader package,
        JsonElement root,
        string? kind,
        IReadOnlyList<string> entryManifests,
        PackageConformanceOptions options,
        Action<PackageConformanceStepResult> add,
        CancellationToken cancellationToken)
    {
        if (string.Equals(kind, "test-executor", StringComparison.OrdinalIgnoreCase))
        {
            var validator = new TestExecutorSchemaValidator(options.TestExecutorSchemaRootPath);
            var providedIds = ReadProvidedIds(root, "providedTestExecutors", "testExecutorId");

            foreach (var entryManifest in entryManifests)
            {
                if (!package.TryReadText(entryManifest, out var content))
                {
                    continue;
                }

                var json = ConvertDocumentToJson(entryManifest, content);
                var result = await validator.ValidateJsonAsync("manifest", json, cancellationToken).ConfigureAwait(false);
                if (result.IsValid)
                {
                    add(Passed("test-executor-manifest-schema", "Test Executor v1 entry manifest passed schema validation.", entryManifest));
                }
                else
                {
                    add(Failed("test-executor-manifest-schema", "Test Executor v1 entry manifest failed schema validation.", entryManifest, result.Errors));
                    continue;
                }

                ValidateIdentityMatchesProvidedIds(json, "executorIdentity", providedIds, entryManifest, "test-executor-id-match", add);
            }

            return;
        }

        if (string.Equals(kind, "implementation", StringComparison.OrdinalIgnoreCase))
        {
            var providedIds = ReadProvidedIds(root, "providedImplementations", "implementationId");
            foreach (var entryManifest in entryManifests)
            {
                if (!package.TryReadText(entryManifest, out var content))
                {
                    continue;
                }

                var json = ConvertDocumentToJson(entryManifest, content);
                ValidateTopLevelIdMatchesProvidedIds(json, providedIds, entryManifest, "implementation-id-match", add);
            }

            return;
        }

        if (string.Equals(kind, "scenario-pack", StringComparison.OrdinalIgnoreCase))
        {
            var providedScenarioIds = ReadProvidedIds(root, "providedScenarios", "scenarioId");
            var providedSuiteIds = ReadProvidedIds(root, "providedSuites", "suiteId");
            JsonSchema? scenarioSchema = null;

            foreach (var entryManifest in entryManifests)
            {
                if (!package.TryReadText(entryManifest, out var content))
                {
                    continue;
                }

                var json = ConvertDocumentToJson(entryManifest, content);
                var normalized = NormalizePackagePath(entryManifest);
                if (normalized.StartsWith("scenarios/", StringComparison.OrdinalIgnoreCase))
                {
                    scenarioSchema ??= await ContractSchemaLoader.LoadAsync(options.ScenarioSchemaPath, cancellationToken).ConfigureAwait(false);
                    var schemaErrors = scenarioSchema.Validate(json);
                    if (schemaErrors.Count == 0)
                    {
                        add(Passed("scenario-manifest-schema", "Scenario entry manifest passed schema validation.", entryManifest));
                    }
                    else
                    {
                        add(Failed("scenario-manifest-schema", "Scenario entry manifest failed schema validation.", entryManifest, schemaErrors.Select(static error => error.ToString()).ToArray()));
                        continue;
                    }

                    ValidateTopLevelIdMatchesProvidedIds(json, providedScenarioIds, entryManifest, "scenario-id-match", add);
                    ValidateScenarioManifestDoesNotContainRunPlanSemantics(json, entryManifest, add);
                    continue;
                }

                if (normalized.StartsWith("suites/", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateTopLevelIdMatchesProvidedIds(json, providedSuiteIds, entryManifest, "suite-id-match", add);
                }
            }
        }
    }

    private static void ValidateScenarioManifestDoesNotContainRunPlanSemantics(
        string json,
        string path,
        Action<PackageConformanceStepResult> add)
    {
        using var document = JsonDocument.Parse(json);
        var forbiddenFields = new[]
        {
            "runPlanId",
            "runPlanVersion",
            "packages",
            "packageReferences",
            "packageId",
            "packageVersion",
            "sha256",
            "suiteIds",
            "scenarioIds",
            "implementationIds",
            "implementations",
            "testExecutorIds",
            "testExecutors",
            "loadProfileId",
            "targetMode",
            "targetNetworkMode",
            "controller",
            "controllerNode",
            "controllerPlacement",
            "jobPolicy"
        };

        var presentFields = forbiddenFields
            .Where(field => document.RootElement.TryGetProperty(field, out _))
            .ToArray();

        if (presentFields.Length == 0)
        {
            add(Passed("scenario-no-run-plan-semantics", "Scenario entry manifest does not contain run-plan selectors or package/controller policy.", path));
            return;
        }

        add(Failed(
            "scenario-no-run-plan-semantics",
            "Scenario entry manifest must not contain run-plan selectors, package references, implementation IDs, executor IDs, load profile selection, or controller policy.",
            path,
            [$"forbidden fields: {string.Join(", ", presentFields)}"]));
    }

    private static IReadOnlyList<string> ReadProvidedIds(JsonElement root, string arrayName, string idName)
    {
        if (!root.TryGetProperty(arrayName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(item => ReadString(item, idName))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    private static void ValidateIdentityMatchesProvidedIds(
        string json,
        string identityPropertyName,
        IReadOnlyList<string> providedIds,
        string path,
        string step,
        Action<PackageConformanceStepResult> add)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(identityPropertyName, out var identity))
        {
            add(Failed(step, $"Entry manifest is missing {identityPropertyName}.", path));
            return;
        }

        var id = ReadString(identity, "id");
        AddIdMatchResult(id, providedIds, path, step, add);
    }

    private static void ValidateTopLevelIdMatchesProvidedIds(
        string json,
        IReadOnlyList<string> providedIds,
        string path,
        string step,
        Action<PackageConformanceStepResult> add)
    {
        using var document = JsonDocument.Parse(json);
        var id = ReadString(document.RootElement, "id");
        AddIdMatchResult(id, providedIds, path, step, add);
    }

    private static void AddIdMatchResult(
        string? id,
        IReadOnlyList<string> providedIds,
        string path,
        string step,
        Action<PackageConformanceStepResult> add)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            add(Failed(step, "Entry manifest must declare an id that matches package provided metadata.", path));
            return;
        }

        if (providedIds.Count == 0 || providedIds.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            add(Passed(step, "Entry manifest id matches package provided metadata.", path));
            return;
        }

        add(Failed(
            step,
            $"Entry manifest id '{id}' is not listed in package provided metadata.",
            path,
            [$"provided ids: {string.Join(", ", providedIds)}"]));
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

    private static string NormalizePackagePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static PackageConformanceStepResult Passed(string step, string message, string? path = null)
    {
        return new PackageConformanceStepResult(step, PackageConformanceOutcome.Passed, message, path);
    }

    private static PackageConformanceStepResult Failed(
        string step,
        string message,
        string? path = null,
        IReadOnlyList<string>? diagnostics = null)
    {
        return new PackageConformanceStepResult(step, PackageConformanceOutcome.Failed, message, path, diagnostics);
    }

    private sealed class PackageContentReader : IDisposable
    {
        private readonly string packagePath;
        private readonly ZipArchive? archive;

        private PackageContentReader(string packagePath, ZipArchive? archive, bool exists)
        {
            this.packagePath = packagePath;
            this.archive = archive;
            Exists = exists;
        }

        public bool Exists { get; }

        public static PackageContentReader Open(string packagePath)
        {
            if (Directory.Exists(packagePath))
            {
                return new PackageContentReader(Path.GetFullPath(packagePath), null, exists: true);
            }

            if (!File.Exists(packagePath))
            {
                return new PackageContentReader(packagePath, null, exists: false);
            }

            try
            {
                return new PackageContentReader(packagePath, ZipFile.OpenRead(packagePath), exists: true);
            }
            catch (InvalidDataException)
            {
                return new PackageContentReader(packagePath, null, exists: false);
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

            var entry = archive.GetEntry(NormalizePackagePath(relativePath));
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
    }
}
