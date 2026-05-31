# Incursa Protocol Lab

ProtocolLab is the public/community repository for a local validation and
benchmarking harness for modern HTTP and transport protocol implementations.
The runner is implementation-neutral: implementations are described through
manifests and started as local processes, Docker targets, or external targets.
The repo is meant for self-serve validation, regression checks, and local
measurement, not for hosted or attested benchmark publication.

## What It Is

- scenario-driven validation and benchmarking for HTTP and transport
  protocols
- an implementation-neutral runner and CLI
- repo-owned adapters, manifests, scenarios, and target servers
- local artifact capture with validation output, summaries, and aggregate JSON
- support for process, Docker, and external-reference targets

## What It Is Not

- official certification or compliance authority
- industry-standard benchmark arbiter
- verified benchmark authority
- production-grade hosted execution
- a private or commercial backend hidden behind the public repo

## Current Supported Scenarios

ProtocolLab v1 is locally operational. Current support includes:

- Kestrel HTTP/1 validation
- Kestrel HTTP/3 validation
- Incursa HTTP/3 validation through the repo-owned adapter project and endpoint target
- managed-lab HTTP/3 comparison with `managed-httpclient-h3-load`
- external-reference HTTP/3 comparison with the repo-owned Docker `h2load --h3` image
- optional Docker target execution for Kestrel, Incursa, Caddy, and nginx
- optional `dotnet-counters` diagnostics and Docker container metrics
- qlog capture for Docker h2load when the image proves `--qlog-file-base`

Local results are shared-host smoke or regression evidence. They are not
publishable benchmark evidence.

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

## Evidence And Measurement Limits

- Validation must pass before benchmark results are accepted.
- `managed-httpclient-h3-load` is a local-lab load generator, not an
  external-reference benchmark tool.
- Docker target execution and local loopback certificates do not make a run
  isolated-host or publishable.
- Raw stdout, stderr, and artifact directories are preserved even when parsing
  fails.
- Comparability warnings are part of the result and should be read before
  drawing conclusions.

See [docs/spec/validation-vs-benchmarking.md](docs/spec/validation-vs-benchmarking.md)
for the detailed separation rules.

## Public And Internal Boundary

This repository is the public/community surface. Any hosted, attested, or
commercial benchmark service would be a separate layer and is not implied by
this repo or its local results.

See [docs/protocol-lab/product-boundaries.md](docs/protocol-lab/product-boundaries.md)
for the conceptual split.

## Documentation

- [docs/README.md](docs/README.md)
- [docs/quickstart.md](docs/quickstart.md)
- [docs/protocol-lab/first-public-release-checklist.md](docs/protocol-lab/first-public-release-checklist.md)

## License

Apache-2.0. See [LICENSE](LICENSE).
