# Incursa Protocol Lab

ProtocolLab is the public/community repository for a local validation and
benchmarking harness for modern HTTP and transport protocol implementations.
It is the canonical community-facing surface. The sibling internal repository
contains private operational extensions, hosted execution work, and
unreleased diagnostics, but the public repo remains the source of truth for
shared contracts, docs, and local validation behavior.

## What It Is

- scenario-driven validation and benchmarking for HTTP and transport
  protocols
- an implementation-neutral runner and CLI
- contract schemas, neutral manifests, scenarios, package tooling, and
  fake/reference fixtures
- local artifact capture with validation output, summaries, and aggregate JSON
- support for process, Docker, and external-reference targets
- public shared contracts that are also published as NuGet packages for
  downstream/internal consumers

## What It Is Not

- official certification or compliance authority
- industry-standard benchmark arbiter
- verified benchmark authority
- production-grade hosted execution
- a private or commercial backend hidden behind the public repo
- a replacement for the internal operational repo

## Current Public Surface

ProtocolLab is being kept contract-first. Current public support includes:

- Adapter Contract v1 under `/protocol-lab/adapter/v1`.
- Test Executor Contract v1 under `/protocol-lab/test-executor/v1`.
- package v2 schemas and tooling for `implementation`, `test-executor`,
  `scenario-pack`, and `toolchain` packages.
- neutral scenario, suite, metric, artifact, endpoint, capability, and
  provenance model definitions.
- fake/reference fixtures and conformance harnesses for contract validation.
- managed-lab and reference test-executor paths used by local fixture proof.
- optional `dotnet-counters` diagnostics and Docker container metrics
- qlog capture for Docker h2load when the image proves `--qlog-file-base`

Local results are shared-host smoke or regression evidence. They are not
publishable benchmark evidence unless the execution profile, metadata, and
claim-level gates are satisfied.

## Build and Validate

For a clean checkout:

```powershell
dotnet tool restore
dotnet restore Incursa.ProtocolLab.sln
dotnet build Incursa.ProtocolLab.sln --no-restore
dotnet test Incursa.ProtocolLab.sln --no-build
```

If you are using the Codex/Workbench environment, run the repository-shape
validation as well:

```powershell
workbench validate --profile core
```

For a one-command bootstrap that also builds the repo-owned h2load image:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bootstrap\Initialize-ProtocolLab.ps1 -BuildH2LoadImage
```

## NuGet Packages

Shared ProtocolLab surfaces are published as NuGet packages for downstream
consumers:

- `Incursa.ProtocolLab.Model`
- `Incursa.ProtocolLab.Adapter.Contracts`
- `Incursa.ProtocolLab.Adapter.Conformance`

Production implementation adapters and test executors are package producers
outside this public contract repository.

## Run Validation

Validate package v2 metadata before handing a component package to any lab:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance package --package <package-root-or-plabpkg>
```

Validate a live Adapter v1 control plane:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance adapter --base-url <adapter-control-plane-url> --scenario-id <supported-scenario-id> --scenario-version 1.0 --protocol <protocol-id>
```

Validate a live Test Executor v1 control plane:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance test-executor --base-url <test-executor-control-plane-url> --test-id <supported-test-id> --scenario-id <supported-scenario-id> --scenario-version 1.0 --protocol <protocol-id>
```

These conformance commands use only public schemas, public contract models, and
local HTTP calls. They do not require `protocol-lab-internal`.

Validate a submitted implementation target and scenario with the CLI:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate --implementations <implementation-id> --scenarios <scenario-id> --protocol <protocol-id>
```

`validate` proves the target and scenario before any benchmark data is
accepted.

## Run a Basic Benchmark

A local benchmark names the implementation, scenario, protocol, and selected
test executor:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --implementations <implementation-id> --scenarios <scenario-id> --protocol <protocol-id> --test-executor <test-executor-id> --concurrency 16 --duration 10 --warmup 2 --repetitions 1
```

For package-backed controller submissions, build or obtain the component
packages first and submit explicit package references:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\lab\Submit-ProtocolLabPackageRun.ps1 `
  -ControllerUri <controller-uri> `
  -PackagePath <implementation-package.plabpkg> `
  -PackageReference <test-executor-package-id:version:sha256> `
  -PackageReference <scenario-package-id:version:sha256> `
  -ImplementationId <implementation-id> `
  -TestExecutorId <test-executor-id> `
  -SuiteId <suite-id> `
  -Protocol <protocol-id>
```

The public repository defines the contracts and neutral catalogs. Production
implementation and test-executor packages live outside the public contract
repository.

## Local Workflow

Use [docs/benchmarking/local-benchmark-workflow.md](docs/benchmarking/local-benchmark-workflow.md)
for the exact commands to run build, test, check, selected benchmark suites,
and publication.

## Publish Completed Runs

Benchmark `run` prepares a public-safe bundle automatically after it writes the
completed run. The bundle reads `aggregate-results.json` and
`evidence-report-v1.json`, sanitizes the report, and writes a staged
publication bundle. It does not upload to R2 by default.

To generate a local run and the corresponding public report bundle in one
command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\New-ProtocolLabPublicReportBundle.ps1
```

Use `publish-report` when you need to restage an existing completed run or
validate the publication plan without running the benchmark again:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- publish-report --run .artifacts\runs\{runId} --output .artifacts\publication\{runId} --visibility public --dry-run
```

Use `--allow-diagnostic-publication` when the bundle is intentionally
diagnostic-only and should remain labeled that way in the output.

To complete the Cloudflare handoff, run
`scripts\publication\Publish-ProtocolLabReport.ps1`. It stages the bundle if
needed, uploads the public files to `public/runs/{runId}/` in the
`protocol-lab-reports` bucket through the R2 S3 API, and can verify the
uploaded run-prefix objects. The downstream site owns processing, indexing,
and latest selection after the R2 objects exist.

To batch upload one or more completed local runs, use the wrapper script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Publish-ProtocolLabRuns.ps1 `
  -RunsRoot .artifacts\runs `
  -PrefixFilter local-workflow `
  -VerifyUploadedObjects
```

This scans completed run directories, uploads each one through the R2 handoff,
and writes `.artifacts\runs\publication-summary.md`.

The workflow consumes these GitHub secrets:

- `CLOUDFLARE_ACCOUNT_ID`
- `R2_ACCESS_KEY_ID`
- `R2_SECRET_ACCESS_KEY`

The R2 endpoint is derived from `CLOUDFLARE_ACCOUNT_ID` unless a different
non-secret endpoint is provided for a jurisdictional bucket.

For local uploads, the uploader first uses existing process environment
variables:

- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_SESSION_TOKEN` when using temporary credentials
- `CLOUDFLARE_ACCOUNT_ID` or `R2_ENDPOINT`
- `AWS_DEFAULT_REGION=auto`

If those are not set, it can load a credentials file named by
`PROTOCOL_LAB_R2_CREDENTIALS_PATH`, a file passed with `-R2CredentialsPath`, or
PowerShell SecretManagement secrets named `ProtocolLab-R2-AccessKeyId`,
`ProtocolLab-R2-SecretAccessKey`, and `ProtocolLab-CloudflareAccountId`.

See [docs/reports/publication-handoff.md](docs/reports/publication-handoff.md)
for the exact layout and validation gates.

## Evidence And Measurement Limits

- Validation must pass before benchmark results are accepted.
- `managed-httpclient-h3-load` is a local-lab load generator, not an
  external-reference benchmark tool.
- Docker target execution and local loopback certificates do not make a run
  isolated-host or publishable.
- The runner records requested load shape, effective load shape, execution
  profile, and report claim level separately. Those fields are not the same
  thing and should be read independently.
- Raw stdout, stderr, and artifact directories are preserved even when parsing
  fails.
- Comparability warnings are part of the result and should be read before
  drawing conclusions.
- `Benchmark` claims are gated. `Verified` remains reserved for future
  controlled/private attestation. Public/community runs should not fabricate
  controlled provenance or publishable status.
- Public report publication bundles are derivatives of completed runs, not
  canonical source material. They must not leak private paths, secrets, or
  internal-only artifacts.

See [docs/spec/validation-vs-benchmarking.md](docs/spec/validation-vs-benchmarking.md)
for the detailed separation rules.

## Public And Internal Boundary

This repository is the public/community surface. The sibling internal
repository carries private operational workflows, hosted execution planning,
and unreleased extensions. Public docs and contracts are authored here first;
the internal repo consumes them instead of silently diverging.

See [docs/protocol-lab/product-boundaries.md](docs/protocol-lab/product-boundaries.md)
for the conceptual split.

## Contributing

Pull requests to this repository require the `Contributor Agreement` status
check. Read [CONTRIBUTOR-AGREEMENT.md](CONTRIBUTOR-AGREEMENT.md) and
[CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request.

## Documentation

- [Contributing](CONTRIBUTING.md) - contribution workflow, checks, and gates
- [Contributor Agreement](CONTRIBUTOR-AGREEMENT.md) - the required public
  agreement text and signing phrase
- [docs/README.md](docs/README.md)
- [docs/quickstart.md](docs/quickstart.md)
- [docs/protocol-lab/first-public-release-checklist.md](docs/protocol-lab/first-public-release-checklist.md)

## License

Apache-2.0. See [LICENSE](LICENSE).
