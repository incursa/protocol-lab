# ProtocolLab v1 Definition of Done

ProtocolLab v1 is the first local operational, reproducible acceptance baseline
for the HTTP/3 harness. It is not a publishable benchmark automation system and
it does not include Incursa performance optimization.

Phase 3A/3B extend the local operational harness with Kestrel and Incursa
Docker target execution. Those extensions are local-only and do not change the
v1 publication claim.

## Required

- `dotnet build Incursa.ProtocolLab.sln` succeeds.
- `dotnet test Incursa.ProtocolLab.sln` passes.
- `dotnet tool restore` works from a clean checkout.
- `dotnet run --project src\Incursa.ProtocolLab.Cli -- check` reports
  actionable tool and manifest state.
- The repo-owned `incursa/protocol-lab-h2load-http3:local` Docker image can be
  built locally with `scripts\build\Build-H2LoadHttp3Image.ps1`.
- Docker Desktop is available for external-reference acceptance unless that
  stage is explicitly skipped.
- Kestrel HTTP/3 validation passes for `http.core.plaintext` and
  `http.core.json`.
- Incursa HTTP/3 validation passes for `http.core.plaintext` and
  `http.core.json` against the repo-owned Incursa HTTP/3 adapter.
- A managed-lab HTTP/3 comparison can run for Kestrel and Incursa.
- An external-reference Docker `h2load --h3` comparison can run for Kestrel and
  Incursa.
- A counter-enabled HTTP/3 comparison can run when `dotnet-counters` is
  restored and available.
- Artifacts are written predictably under `.artifacts\runs\{runId}`.
- Run `summary.md` and `aggregate-results.json` files are produced for
  benchmark runs.
- Evidence class and comparability status are present in result and aggregate
  JSON.
- Missing optional tools produce honest unsupported, unavailable, skipped, or
  warning output instead of fabricated metrics.

## Deferred Beyond v1

- Raw QUIC workloads.
- WebTransport workloads.
- MASQUE workloads.
- Database workloads.
- nginx and quic-go target execution. Caddy is optional post-v1 local Docker
  evidence and is not required for default v1 acceptance.
- Network impairment.
- Publishable isolated-host benchmark automation.
- Incursa optimization work.

## Acceptance Commands

Bootstrap:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bootstrap\Initialize-ProtocolLab.ps1 -BuildH2LoadImage
```

Acceptance:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -RunIdPrefix local-v1-acceptance `
  -DurationSeconds 5 `
  -WarmupSeconds 1 `
  -Repetitions 1
```

The acceptance script writes per-run summaries and a consolidated
`.artifacts\runs\index.md`.
