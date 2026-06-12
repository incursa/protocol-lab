# ProtocolLab Component Package v2

ProtocolLab Component Package v2 extends the trusted `.plabpkg` format beyond implementation packages. It lets a job reference separately versioned implementations, test executors, scenario packs, and later toolchains while keeping each package identified by `packageId`, `packageVersion`, and SHA-256.

V2 packages do not make a run publishable by themselves. They provide bytes and catalog entries for a trusted lab worker; the run still needs the normal ProtocolLab validation, artifact capture, and evidence classification.

For the execution vocabulary around test cases, scenarios, scenario packs,
suites, load profiles, test executors, implementation packages, toolchains,
and run plans, see
[Test Case And Run Plan Model](../architecture/test-case-run-plan-model.md).

## Component Kinds

The root `protocol-lab-package.json` uses `schemaVersion: "protocol-lab-package-v2"` and one of these `kind` values:

The canonical JSON Schema is `schemas/package/v2/package.schema.json`.

| Kind | Purpose | Required provided metadata |
| --- | --- | --- |
| `implementation` | Provides one or more target implementation manifests. | `providedImplementations` |
| `test-executor` | Provides one or more test executor manifests and executable payloads. | `providedTestExecutors` |
| `scenario-pack` | Provides suites and/or scenarios. | `providedSuites` or `providedScenarios` |
| `toolchain` | Reserved for shared worker prerequisites. | `dependencies.requiredCapabilities` |

V1 packages are implementation-only packages. New component packages should use v2.

Runtime component packages (`implementation`, `test-executor`, and
`scenario-pack`) must include at least one `entryManifests` value. Toolchain
packages may be metadata-only for now, but still use the same root manifest
shape. Entry manifests, environment entrypoint paths, and working directories
must be package-relative paths. Absolute paths, drive-qualified paths, and
path traversal segments are invalid.

Entry manifest layout is part of the contract:

- `implementation` packages use `implementations/`.
- `test-executor` packages use `test-executors/`.
- `scenario-pack` packages use `scenarios/` and/or `suites/`.

`load-runner`, load-tool package kinds, and `providedLoadTools` are legacy
draft names and are not valid package v2 semantics.

## Local Package Validation

Validate a package source directory or `.plabpkg` archive with the public CLI:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package <package-root-or-plabpkg>
```

The command validates the root manifest against
`schemas/package/v2/package.schema.json`, checks entry-manifest paths and
existence, validates `test-executor` entry manifests against
`schemas/test-executor/v1/manifest.schema.json`, validates scenario entries
against `schemas/scenario.schema.json`, and checks that entry manifest IDs
match the package `provided*` metadata.

Neutral examples live under `fixtures/public-contracts/packages/`:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\neutral-test-executor
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\neutral-adapter-implementation
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\neutral-scenario-pack
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package fixtures\public-contracts\packages\http1-core-scenario-pack
```

The implementation fixture proves package v2 metadata and entry layout only.
Adapter v1 lifecycle behavior is proven by running
`conformance adapter --base-url <adapter-control-plane-url>` against the live
adapter control plane.

## IDs And Catalog Entries

Package IDs, implementation IDs, test-executor IDs, scenario IDs, suite IDs,
and capability names are stable public contract keys. They should use the
package v2 token shape: start with a letter or digit and then use only
letters, digits, underscore, period, colon, or hyphen.

`entryManifests` names package-local catalog documents. `provided*` metadata
names the public IDs that schedulers may use for compatibility matching. Both
must agree. Scenario entry manifests may keep catalog subdirectories such as
`scenarios/quic/transport/duplex-streams.yaml`; the scenario document `id`
still has to match the `providedScenarios[].scenarioId` value. For example, a
`test-executor` package that advertises
`providedTestExecutors[].testExecutorId: quic-go-raw-load` should provide a
`test-executors/quic-go-raw-load.yaml` manifest that implements Test Executor
Contract v1 for that executor.

Scenario packs may provide suites, scenarios, or both. A suite-level
`testExecutors` list constrains which test executors can satisfy that suite.
If the selected package set does not provide the requested implementation,
test executor, suite, scenario, protocol, endpoint binding, or capability,
the controller must reject the job or the worker must return an explicit
unsupported/unavailable outcome. It must not infer a replacement from names,
package kind, runtime, or protocol family.

The `http1-core-scenario-pack` fixture is a minimal scenario-pack example. It
provides `http.core.plaintext`, `http.core.json`,
`http.payload.bytes.1kb`, and an `http1-core-smoke` suite without
implementation IDs, test executor IDs, package references, or controller/job
policy in the scenario files.

## Compatibility Semantics

A package-backed job is compatible only when the selected job inputs can be satisfied by the referenced packages:

- The selected implementation id must come from a v1 implementation package or a v2 `providedImplementations` entry.
- The selected test executor must come from a v2 `providedTestExecutors` entry.
- The selected suite or scenario must come from a v2 `providedSuites` or `providedScenarios` entry.
- The selected protocol must intersect with the implementation, test executor, and suite/scenario protocol declarations.
- Any scenario-pack suite `testExecutors` declaration must include the selected test executor when the suite constrains executor choice.
- Required capabilities declared by package dependencies or provided test executors must be available on the worker.

Workers and controllers must reject incompatible component sets. They must not silently substitute another implementation, another test executor, or another protocol lane. In particular, a raw QUIC job that selects `protocol: quic` and a raw QUIC test-executor package must not fall back to `managed-httpclient-h3-load`.

Package-backed job submissions should carry explicit selected IDs, including
`implementationIds`, `testExecutorIds`, `suiteIds` or `scenarioIds`, and
`protocols`. Package references provide available components; they do not
authorize the controller to pick a compatible-looking executor implicitly.

## Relationship To Run Plans

Package v2 defines component archives. A run plan is a separate repeatable job
manifest that references exact package identities and SHA-256 values, then
selects implementation IDs, test executor IDs, suite or scenario IDs,
protocols, load profile, and execution policy.

Do not encode run-plan semantics inside a package manifest. Package manifests
declare what the package provides. Run plans declare which package-provided
components should be used for a specific repeatable run.

## Provenance Artifacts

Hosted package-backed results should record:

- each referenced package's `packageId`, `packageVersion`, SHA-256, and `kind`
- selected entrypoint and materialized working directory
- effective implementation, test-executor, suite, and scenario manifest ids
- the generic `load-tool-command.txt` artifact
- stdout/stderr and `load-tool-execution.json`

The legacy `h2load-command.txt` artifact remains only for compatibility with older h2load consumers. New load tools, including raw QUIC process tools, should use `load-tool-command.txt` as the durable command artifact.
