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
- repo-owned adapters, manifests, scenarios, and target servers
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

## Current Supported Scenarios

ProtocolLab v1 is locally operational. Current support includes:

- Kestrel HTTP/1 validation
- Kestrel HTTP/3 validation
- Incursa HTTP/3 validation through the repo-owned adapter project and endpoint target
- raw QUIC fixture-only adapter coverage for protocol-boundary work
- managed-lab HTTP/3 comparison with `managed-httpclient-h3-load`, including the full stable local comparison suite across core, payload, headers, and upload scenarios
- external-reference HTTP/3 comparison with the repo-owned Docker `h2load --h3` image
- optional Docker target execution for Kestrel, Incursa, Caddy, nginx, and quic-go
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

Runners, servers, and implementation targets remain source-only in this repo.

## Run Validation

Validate a specific target and scenario with the CLI:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- validate --implementations kestrel-http3 --scenarios http.core.plaintext --protocol h3
```

`validate` proves the target and scenario before any benchmark data is
accepted.

## Run a Basic Benchmark

A simple local benchmark uses the managed HTTP/3 load generator and does not
require Docker:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --implementations kestrel-http3 --scenarios http.core.plaintext,http.core.json --protocol h3 --load-tool managed-httpclient-h3-load --concurrency 16 --duration 10 --warmup 2 --repetitions 1
```

For the external-reference H3 path, use the repo-owned Docker `h2load --h3`
image:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --implementations kestrel-http3,incursa-http3 --scenarios http.core.plaintext,http.core.json --protocol h3 --load-tool h2load --load-tool-mode docker
```

For broader local comparison coverage, use `suites/h3-local-v1-comparison.yaml` with `--load-profile local-comparison`; it includes quic-go alongside Kestrel and Incursa while covering the full stable H3 app matrix across core, payload, headers, and upload scenarios.

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
`protocol-lab-reports` bucket through the R2 S3 API, verifies the uploaded
payload before advancing the registry/latest pointers, and writes the
searchable metadata into D1 through the Cloudflare D1 REST API.

To batch upload one or more completed local runs, use the wrapper script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publication\Publish-ProtocolLabRuns.ps1 `
  -RunsRoot .artifacts\runs `
  -PrefixFilter local-workflow `
  -VerifyPublishedRuns
```

This scans completed run directories, publishes each one through the existing
Cloudflare handoff, and writes `.artifacts\runs\publication-summary.md`.

The workflow consumes these GitHub secrets:

- `CLOUDFLARE_API_TOKEN`
- `CLOUDFLARE_ACCOUNT_ID`
- `PROTOCOL_LAB_DB_ID`
- `R2_ACCESS_KEY_ID`
- `R2_SECRET_ACCESS_KEY`

The R2 endpoint is derived from `CLOUDFLARE_ACCOUNT_ID` unless a different
non-secret endpoint is provided for a jurisdictional bucket.

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
