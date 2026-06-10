# ProtocolLab v1 Local Operational

Date: 2026-05-27

Suggested tag: `v1-local-operational`

## Scope

This release freezes ProtocolLab as a locally operational v1 harness for HTTP/3
validation and local benchmark acceptance. It is a clean-checkout bootstrap,
check, acceptance, and artifact-review milestone.

It does not claim publishable benchmark readiness and does not include Incursa
performance optimization.

## What Works

- Clean bootstrap through `scripts\bootstrap\Initialize-ProtocolLab.ps1`.
- Actionable `check` output for .NET, repo-local tools, Docker, h2load, curl,
  managed H3 proof/load, `dotnet-counters`, and manifests.
- Kestrel HTTP/3 validation for `http.core.plaintext` and `http.core.json`.
- Managed-lab HTTP/3 comparison for Kestrel and quic-go public reference
  targets.
- Docker h2load external-reference HTTP/3 comparison for public reference
  targets.
- Optional quic-go HTTP/3 Docker target support for the expanded local
  comparison suite.
- Counter-enabled Docker h2load HTTP/3 comparison when `dotnet-counters` is
  restored and available.
- Consolidated markdown run index under `.artifacts\runs\index.md`.
- Per-run `summary.md` and `aggregate-results.json`.

## Acceptance Command

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\acceptance\Invoke-ProtocolLabAcceptance.ps1 `
  -RunIdPrefix local-v1-acceptance `
  -DurationSeconds 5 `
  -WarmupSeconds 1 `
  -Repetitions 1
```

Bootstrap first when preparing a clean checkout:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\bootstrap\Initialize-ProtocolLab.ps1 -BuildH2LoadImage
```

## Artifact Locations

- Run root: `.artifacts\runs\{runId}`
- Consolidated index: `.artifacts\runs\index.md`
- Benchmark summary: `.artifacts\runs\{runId}\summary.md`
- Aggregate JSON: `.artifacts\runs\{runId}\aggregate-results.json`
- Raw command output: per-cell `load-tool.stdout.txt`,
  `load-tool.stderr.txt`, `target.stdout.txt`, and `target.stderr.txt`

`.artifacts\runs` is ignored by git and benchmark artifacts should not be
committed.

## Evidence Warning

Local v1 results are useful for smoke validation, local regression, and
profiling direction. They are not publishable benchmark evidence.

Expected local evidence classes include `local-lab` and
`external-reference-local`, usually with comparability warnings for shared host
execution, Docker host rewrite, single repetition, missing load-generator CPU
capture, and non-isolated resources.

## Known Limitations

- Docker Desktop is required for the external-reference h2load acceptance path.
- curl may lack `--http3-only`; ProtocolLab falls back to managed exact HTTP/3
  validation when available.
- Counter capture depends on repo-local `dotnet-counters` restore and runtime
  process resolution.
- Results are local shared-host measurements.

## Deferred Work

- Raw QUIC workloads.
- WebTransport.
- MASQUE.
- Database workloads.
- nginx target execution.
- Network impairment.
- Publishable isolated-host benchmark automation.
- Implementation optimization work.

## Tag Command

```powershell
git tag -a v1-local-operational -m "ProtocolLab v1 local operational harness"
```
