// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Incursa.ProtocolLab.Adapter.Conformance;
using NJsonSchema;
using YamlDotNet.Serialization;

namespace Incursa.ProtocolLab.Tests;

public sealed class PublicContractSchemaTests
{
    [Fact]
    public async Task Package_v2_schema_accepts_test_executor_package()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "package", "v2", "package.schema.json"));
        var payload = """
        {
          "schemaVersion": "protocol-lab-package-v2",
          "packageId": "protocol-lab-raw-quic-test-executor",
          "packageVersion": "dev",
          "kind": "test-executor",
          "entryManifests": ["test-executors/quic-go-raw-load.yaml"],
          "providedTestExecutors": [
            {
              "testExecutorId": "quic-go-raw-load",
              "displayName": "quic-go Raw QUIC Load",
              "protocols": ["quic"],
              "scenarios": ["quic.transport.multiplex.100x64kb"],
              "tests": ["raw-quic-throughput"]
            }
          ],
          "environments": [
            {
              "os": "linux",
              "arch": "x64",
              "entrypoint": {
                "kind": "process",
                "path": "bin/linux-x64/quic-go-raw-load",
                "arguments": [],
                "workingDirectory": "."
              }
            }
          ],
          "dependencies": {
            "requiresDotNet": false,
            "requiresDocker": false,
            "requiresPwsh": false,
            "requiresBash": false,
            "requiresGo": false,
            "requiredCapabilities": []
          }
        }
        """;

        var errors = schema.Validate(payload);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task Package_v2_schema_rejects_test_executor_entry_manifest_under_load_tools()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "package", "v2", "package.schema.json"));
        var payload = """
        {
          "schemaVersion": "protocol-lab-package-v2",
          "packageId": "fixture-test-executor",
          "packageVersion": "dev",
          "kind": "test-executor",
          "entryManifests": ["load-tools/fixture.yaml"],
          "providedTestExecutors": [
            { "testExecutorId": "fixture-test-executor", "protocols": ["h3"] }
          ],
          "environments": [
            {
              "os": "linux",
              "arch": "x64",
              "entrypoint": {
                "kind": "process",
                "path": "bin/linux-x64/fixture-test-executor",
                "arguments": [],
                "workingDirectory": "."
              }
            }
          ],
          "dependencies": {
            "requiresDotNet": false,
            "requiresDocker": false,
            "requiresPwsh": false,
            "requiresBash": false
          }
        }
        """;

        var errors = schema.Validate(payload);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.ToString().Contains("entryManifests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Package_v2_schema_rejects_load_runner_package_kind()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "package", "v2", "package.schema.json"));
        var payload = """
        {
          "schemaVersion": "protocol-lab-package-v2",
          "packageId": "old-raw-quic-load-runner",
          "packageVersion": "dev",
          "kind": "load-runner",
          "entryManifests": ["load-tools/quic-go-raw-load.yaml"],
          "providedLoadTools": [
            { "loadToolId": "quic-go-raw-load", "protocols": ["quic"] }
          ],
          "environments": [
            {
              "os": "linux",
              "arch": "x64",
              "entrypoint": {
                "kind": "process",
                "path": "bin/linux-x64/quic-go-raw-load",
                "arguments": [],
                "workingDirectory": "."
              }
            }
          ],
          "dependencies": {
            "requiresDotNet": false,
            "requiresDocker": false,
            "requiresPwsh": false,
            "requiresBash": false
          }
        }
        """;

        var errors = schema.Validate(payload);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => (error.Path ?? "").Contains("kind", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Package_v2_schema_rejects_empty_entry_manifests_for_runtime_component_packages()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "package", "v2", "package.schema.json"));
        var payload = """
        {
          "schemaVersion": "protocol-lab-package-v2",
          "packageId": "fixture-empty-entry-manifests",
          "packageVersion": "dev",
          "kind": "implementation",
          "entryManifests": [],
          "providedImplementations": [
            {
              "implementationId": "fixture-implementation",
              "protocols": ["h3"]
            }
          ],
          "environments": [
            {
              "os": "linux",
              "arch": "x64",
              "entrypoint": {
                "kind": "process",
                "path": "bin/linux-x64/fixture",
                "arguments": [],
                "workingDirectory": "."
              }
            }
          ],
          "dependencies": {
            "requiresDotNet": false,
            "requiresDocker": false,
            "requiresPwsh": false,
            "requiresBash": false
          }
        }
        """;

        var errors = schema.Validate(payload);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.ToString().Contains("entryManifests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Package_v2_schema_allows_toolchain_packages_without_entry_manifests()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "package", "v2", "package.schema.json"));
        var payload = """
        {
          "schemaVersion": "protocol-lab-package-v2",
          "packageId": "fixture-toolchain",
          "packageVersion": "dev",
          "kind": "toolchain",
          "entryManifests": [],
          "environments": [
            {
              "os": "linux",
              "arch": "x64",
              "entrypoint": {
                "kind": "bash",
                "path": "scripts/setup.sh",
                "arguments": [],
                "workingDirectory": "."
              }
            }
          ],
          "dependencies": {
            "requiresDotNet": false,
            "requiresDocker": false,
            "requiresPwsh": false,
            "requiresBash": true,
            "requiredCapabilities": []
          }
        }
        """;

        var errors = schema.Validate(payload);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("C:/tools/fixture-test-executor.exe")]
    [InlineData("/usr/local/bin/fixture-test-executor")]
    [InlineData("https://example.invalid/fixture-test-executor")]
    [InlineData("bin/../fixture-test-executor")]
    public async Task Package_v2_schema_rejects_non_package_relative_entrypoint_paths(string entrypointPath)
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "package", "v2", "package.schema.json"));
        var payload = $$"""
        {
          "schemaVersion": "protocol-lab-package-v2",
          "packageId": "fixture-entrypoint-path",
          "packageVersion": "dev",
          "kind": "test-executor",
          "entryManifests": ["test-executors/fixture.yaml"],
          "providedTestExecutors": [
            {
              "testExecutorId": "fixture-test-executor",
              "protocols": ["h3"],
              "tests": ["http.core.plaintext"]
            }
          ],
          "environments": [
            {
              "os": "linux",
              "arch": "x64",
              "entrypoint": {
                "kind": "process",
                "path": "{{entrypointPath}}",
                "arguments": [],
                "workingDirectory": "."
              }
            }
          ],
          "dependencies": {
            "requiresDotNet": false,
            "requiresDocker": false,
            "requiresPwsh": false,
            "requiresBash": false
          }
        }
        """;

        var errors = schema.Validate(payload);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.ToString().Contains("entrypoint.path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Test_executor_manifest_schema_accepts_representative_manifest()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "test-executor", "v1", "manifest.schema.json"));
        var payload = """
        {
          "executorIdentity": {
            "id": "quic-go-raw-load",
            "name": "quic-go Raw QUIC Load",
            "version": "dev"
          },
          "versionCompatibility": {
            "contractVersion": "test-executor-v1",
            "compatibleContractVersions": ["test-executor-v1"]
          },
          "claimedCapabilities": [
            { "id": "quic.transport", "status": "supported" }
          ],
          "supportedTestSelectors": [
            { "selectorType": "test-id", "expression": "raw-quic-throughput" }
          ],
          "supportedScenarioSelectors": [
            { "selectorType": "scenario-id", "expression": "quic.transport.multiplex.100x64kb" }
          ],
          "supportedProtocolFamilies": ["quic"],
          "supportedExecutionModes": ["process"],
          "requiredTargetEndpointBindings": [
            {
              "bindingId": "target",
              "purpose": "raw-quic-server",
              "endpointType": "quic",
              "protocols": ["quic"]
            }
          ],
          "supportedArtifactTypes": [
            { "type": "load.stdout.log" }
          ],
          "metricsAvailability": {
            "available": true,
            "availableKinds": ["summary"]
          }
        }
        """;

        var errors = schema.Validate(payload);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task Run_plan_v1_schema_accepts_representative_package_backed_plan()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "run-plan", "v1", "run-plan.schema.json"));
        var payload = """
        {
          "schemaVersion": "protocol-lab-run-plan-v1",
          "runPlanId": "h3-core-smoke-reference",
          "runPlanVersion": "2026.06.10",
          "displayName": "HTTP/3 core smoke reference run",
          "packages": [
            {
              "packageId": "protocol-lab-h3-core-scenarios",
              "packageVersion": "2026.06.10",
              "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
            },
            {
              "packageId": "protocol-lab-managed-h3-test-executor",
              "packageVersion": "2026.06.10",
              "sha256": "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"
            },
            {
              "packageId": "kestrel-http3",
              "packageVersion": "2026.06.10",
              "sha256": "1111111111111111111111111111111111111111111111111111111111111111"
            }
          ],
          "suiteIds": ["h3-local-v1"],
          "implementationIds": ["kestrel-http3"],
          "testExecutorIds": ["managed-httpclient-h3-load"],
          "protocols": ["h3"],
          "loadProfileId": "smoke",
          "targetMode": "process",
          "targetNetworkMode": "published-port",
          "requiredCapabilities": [
            {
              "name": "protocol-lab-cli",
              "value": "true"
            }
          ],
          "comparisonGroups": [
            {
              "groupId": "h3-core",
              "suiteIds": ["h3-local-v1"],
              "sameExecutorRequired": true,
              "sameLoadProfileRequired": true
            }
          ],
          "publicationIntent": "local-only",
          "notes": "Reference package-backed smoke run."
        }
        """;

        var errors = schema.Validate(payload);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task Run_plan_v1_schema_accepts_public_valid_fixtures()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "run-plan", "v1", "run-plan.schema.json"));
        var fixtureRoot = Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "run-plans", "valid");
        var fixtures = Directory.EnumerateFiles(fixtureRoot, "*.json").ToArray();

        Assert.NotEmpty(fixtures);
        foreach (var fixture in fixtures)
        {
            var errors = schema.Validate(await File.ReadAllTextAsync(fixture));
            Assert.Empty(errors);
        }
    }

    [Fact]
    public async Task Run_plan_v1_schema_rejects_public_invalid_fixtures()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "run-plan", "v1", "run-plan.schema.json"));
        var fixtureRoot = Path.Combine(TestPaths.RepoRoot, "fixtures", "public-contracts", "run-plans", "invalid");
        var fixtures = Directory.EnumerateFiles(fixtureRoot, "*.json").ToArray();

        Assert.NotEmpty(fixtures);
        foreach (var fixture in fixtures)
        {
            var errors = schema.Validate(await File.ReadAllTextAsync(fixture));
            Assert.NotEmpty(errors);
        }
    }

    [Fact]
    public async Task Run_plan_v1_schema_rejects_package_reference_without_sha256()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "run-plan", "v1", "run-plan.schema.json"));
        var payload = """
        {
          "schemaVersion": "protocol-lab-run-plan-v1",
          "runPlanId": "missing-package-hash",
          "runPlanVersion": "2026.06.10",
          "packages": [
            {
              "packageId": "protocol-lab-h3-core-scenarios",
              "packageVersion": "2026.06.10"
            }
          ],
          "scenarioIds": ["http.core.plaintext"],
          "implementationIds": ["kestrel-http3"],
          "testExecutorIds": ["managed-httpclient-h3-load"],
          "protocols": ["h3"],
          "loadProfileId": "smoke",
          "targetMode": "process",
          "targetNetworkMode": "published-port",
          "requiredCapabilities": []
        }
        """;

        var errors = schema.Validate(payload);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.ToString().Contains("sha256", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Run_plan_v1_schema_rejects_inline_scenario_behavior()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "run-plan", "v1", "run-plan.schema.json"));
        var payload = """
        {
          "schemaVersion": "protocol-lab-run-plan-v1",
          "runPlanId": "inline-scenario",
          "runPlanVersion": "2026.06.10",
          "packages": [
            {
              "packageId": "protocol-lab-h3-core-scenarios",
              "packageVersion": "2026.06.10",
              "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
            }
          ],
          "scenarioIds": ["http.core.plaintext"],
          "implementationIds": ["kestrel-http3"],
          "testExecutorIds": ["managed-httpclient-h3-load"],
          "protocols": ["h3"],
          "loadProfileId": "smoke",
          "targetMode": "process",
          "targetNetworkMode": "published-port",
          "requiredCapabilities": [],
          "scenarios": [
            {
              "id": "http.core.plaintext",
              "path": "/plaintext"
            }
          ]
        }
        """;

        var errors = schema.Validate(payload);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.ToString().Contains("scenarios", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Run_plan_v1_schema_requires_suite_or_scenario_selection()
    {
        var schema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "run-plan", "v1", "run-plan.schema.json"));
        var payload = """
        {
          "schemaVersion": "protocol-lab-run-plan-v1",
          "runPlanId": "missing-work-selection",
          "runPlanVersion": "2026.06.10",
          "packages": [
            {
              "packageId": "protocol-lab-h3-core-scenarios",
              "packageVersion": "2026.06.10",
              "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
            }
          ],
          "implementationIds": ["kestrel-http3"],
          "testExecutorIds": ["managed-httpclient-h3-load"],
          "protocols": ["h3"],
          "loadProfileId": "smoke",
          "targetMode": "process",
          "targetNetworkMode": "published-port",
          "requiredCapabilities": []
        }
        """;

        var errors = schema.Validate(payload);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Public_contract_schema_files_are_valid_json()
    {
        var schemaFiles = Directory.EnumerateFiles(Path.Combine(TestPaths.RepoRoot, "schemas", "test-executor", "v1"), "*.json")
            .Concat(Directory.EnumerateFiles(Path.Combine(TestPaths.RepoRoot, "schemas", "package", "v2"), "*.json"))
            .Concat(Directory.EnumerateFiles(Path.Combine(TestPaths.RepoRoot, "schemas", "run-plan", "v1"), "*.json"));

        foreach (var schemaFile in schemaFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(schemaFile));
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        }
    }

    [Fact]
    public async Task Raw_quic_component_package_builder_emits_schema_valid_public_packages()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"protocol-lab-raw-quic-packages-{Guid.NewGuid():N}");
        try
        {
            var version = $"test-{Guid.NewGuid():N}";
            var result = await RunPowerShellAsync(
                Path.Combine(TestPaths.RepoRoot, "scripts", "lab", "New-ProtocolLabRawQuicComponentPackages.ps1"),
                "-PackageVersion",
                version,
                "-SourceBackedTestExecutor",
                "-OutputRoot",
                outputRoot,
                "-Force");

            Assert.True(result.ExitCode == 0, result.Output);

            using var document = JsonDocument.Parse(result.Output);
            var testExecutorPackagePath = document.RootElement.GetProperty("testExecutorPackage").GetProperty("path").GetString();
            var scenarioPackagePath = document.RootElement.GetProperty("scenarioPackage").GetProperty("path").GetString();

            Assert.False(string.IsNullOrWhiteSpace(testExecutorPackagePath));
            Assert.False(string.IsNullOrWhiteSpace(scenarioPackagePath));

            var packageSchema = await LoadSchemaAsync(Path.Combine(TestPaths.RepoRoot, "schemas", "package", "v2", "package.schema.json"));
            var testExecutorManifest = await ValidatePackageRootAsync(packageSchema, testExecutorPackagePath!, expectedKind: "test-executor");
            var scenarioPackageManifest = await ValidatePackageRootAsync(packageSchema, scenarioPackagePath!, expectedKind: "scenario-pack");

            Assert.Contains("test-executors/quic-go-raw-load.yaml", testExecutorManifest.EntryNames, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("suites/quic-transport-v1-comparison.yaml", scenarioPackageManifest.EntryNames, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("scenarios/quic/transport/duplex-streams.yaml", scenarioPackageManifest.EntryNames, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("scenarios/quic/transport/multiplex-100-streams.yaml", scenarioPackageManifest.EntryNames, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(scenarioPackageManifest.EntryNames, entry => entry.Contains("handshake-cold", StringComparison.OrdinalIgnoreCase));

            var testExecutorYaml = ReadZipEntry(testExecutorPackagePath!, "test-executors/quic-go-raw-load.yaml");
            var testExecutorJson = ConvertYamlToJson(testExecutorYaml);

            var validator = new TestExecutorSchemaValidator(Path.Combine(TestPaths.RepoRoot, "schemas", "test-executor", "v1"));
            var validation = await validator.ValidateJsonAsync("manifest", testExecutorJson);

            Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
            using var testExecutorDocument = JsonDocument.Parse(testExecutorJson);
            var root = testExecutorDocument.RootElement;
            Assert.Equal("test-executor-v1", root.GetProperty("versionCompatibility").GetProperty("contractVersion").GetString());
            Assert.Contains(root.GetProperty("supportedTestSelectors").EnumerateArray(), selector =>
                selector.GetProperty("selectorType").GetString() == "test-id" &&
                selector.GetProperty("expression").GetString() == "quic.transport.multiplex.100x64kb");
            Assert.Contains(root.GetProperty("supportedScenarioSelectors").EnumerateArray(), selector =>
                selector.GetProperty("selectorType").GetString() == "scenario-id" &&
                selector.GetProperty("expression").GetString() == "quic.transport.duplex-streams");
            Assert.Equal(["quic"], root.GetProperty("supportedProtocolFamilies").EnumerateArray().Select(protocol => protocol.GetString() ?? "").ToArray());
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private static async Task<JsonSchema> LoadSchemaAsync(string schemaPath)
    {
        var schemaJson = await File.ReadAllTextAsync(schemaPath);
        schemaJson = await ExpandSchemaDefinitionsAsync(schemaPath, schemaJson);
        return await JsonSchema.FromJsonAsync(schemaJson, schemaPath);
    }

    private static async Task<string> ExpandSchemaDefinitionsAsync(string schemaPath, string schemaJson)
    {
        var schemaNode = JsonNode.Parse(schemaJson)!.AsObject();
        var definitions = schemaNode["definitions"] as JsonObject ?? new JsonObject();

        if (schemaNode["$defs"] is JsonObject localDefs)
        {
            foreach (var (key, value) in localDefs)
            {
                definitions[key] = value?.DeepClone();
            }
        }

        var commonSchemaPath = Path.Combine(Path.GetDirectoryName(schemaPath)!, "common.schema.json");
        if (File.Exists(commonSchemaPath) && !Path.GetFileName(schemaPath).Equals("common.schema.json", StringComparison.OrdinalIgnoreCase))
        {
            var commonNode = JsonNode.Parse(await File.ReadAllTextAsync(commonSchemaPath))!.AsObject();
            if (commonNode["$defs"] is JsonObject commonDefs)
            {
                foreach (var (key, value) in commonDefs)
                {
                    definitions[key] ??= value?.DeepClone();
                }
            }
        }

        if (definitions.Count > 0)
        {
            schemaNode["definitions"] = definitions;
        }

        schemaNode.Remove("$defs");
        RewriteReferences(schemaNode);
        return schemaNode.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void RewriteReferences(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToArray())
                {
                    if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        const string CommonDefsPrefix = "./common.schema.json#/$defs/";
                        const string LocalDefsPrefix = "#/$defs/";

                        if (text.StartsWith(CommonDefsPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            obj[property.Key] = "#/definitions/" + text[CommonDefsPrefix.Length..];
                            continue;
                        }

                        if (text.StartsWith(LocalDefsPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            obj[property.Key] = "#/definitions/" + text[LocalDefsPrefix.Length..];
                            continue;
                        }
                    }

                    if (property.Value is not null)
                    {
                        RewriteReferences(property.Value);
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        RewriteReferences(item);
                    }
                }

                break;
        }
    }

    private static async Task<(string[] EntryNames, JsonElement Manifest)> ValidatePackageRootAsync(
        JsonSchema schema,
        string packagePath,
        string expectedKind)
    {
        Assert.True(File.Exists(packagePath), $"Expected package file to exist: {packagePath}");

        var manifestJson = ReadZipEntry(packagePath, "protocol-lab-package.json");
        var errors = schema.Validate(manifestJson);
        Assert.True(errors.Count == 0, $"{Path.GetFileName(packagePath)} failed package schema validation: {string.Join("; ", errors.Select(error => error.ToString()))}");

        using var document = JsonDocument.Parse(manifestJson);
        Assert.Equal(expectedKind, document.RootElement.GetProperty("kind").GetString());

        var entryNames = document.RootElement.GetProperty("entryManifests")
            .EnumerateArray()
            .Select(entry => entry.GetString()!)
            .ToArray();

        foreach (var entryName in entryNames)
        {
            Assert.False(string.IsNullOrWhiteSpace(ReadZipEntry(packagePath, entryName)), $"{packagePath} should contain {entryName}.");
        }

        return (entryNames, document.RootElement.Clone());
    }

    private static string ReadZipEntry(string packagePath, string entryName)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.GetEntry(entryName.Replace('\\', '/'));
        Assert.NotNull(entry);

        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    private static string ConvertYamlToJson(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithAttemptingUnquotedStringTypeDeserialization()
            .Build();
        var serializer = new SerializerBuilder().JsonCompatible().Build();
        var yamlObject = deserializer.Deserialize(new StringReader(yaml));
        return serializer.Serialize(yamlObject);
    }

    private static async Task<(int ExitCode, string Output)> RunPowerShellAsync(string scriptPath, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            WorkingDirectory = TestPaths.RepoRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PowerShell.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout + stderr);
    }
}
