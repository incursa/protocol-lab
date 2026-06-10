# Runtime Counter Capture

Phase 2K adds optional .NET runtime counter capture for process-based targets.
It is diagnostic evidence for existing benchmark cells, not a new benchmark
family and not an implementation optimization phase.

## Why Process Identity Matters

Earlier target process metrics were captured from the process ProtocolLab
started. For manifests launched through `dotnet run --project`, that process can
be the `dotnet run` wrapper rather than the actual server process. CPU,
allocation, GC, and thread-pool attribution to the wrapper is not trustworthy.

Phase 2K therefore records both identities:

- the root process started by ProtocolLab
- the resolved diagnostic process used for counters

If the root process appears to be a `dotnet run` wrapper and no actual server
process is resolved, counter capture is skipped and the result records a
low-confidence unresolved diagnostic target.

## Target Startup

For `dotnet` process manifests with a project path, ProtocolLab now prefers:

```text
dotnet exec path\to\Target.dll
```

The runner resolves `TargetPath` with MSBuild and builds the project if the DLL
is missing. If that direct startup path cannot be established, the runner falls
back to the existing `dotnet run --project` behavior and records a warning.

This keeps manifests neutral: ProtocolLab still starts `dotnet` and does not
reference concrete implementation assemblies directly.

## Diagnostic Target Model

Per-cell `diagnostic-target.json` records:

- `rootProcessId`
- `resolvedProcessId`
- `resolvedProcessName`
- `resolutionStrategy`
- `commandLine`
- `executablePath`
- `workingDirectory`
- `confidence`
- `warnings`
- `errors`

Current resolution strategies include `root-process` for direct server startup
and `unresolved` when a wrapper process cannot safely be used for counters.

## Counter Tool

Counter capture is opt-in:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run `
  --implementations kestrel-http3 `
  --scenarios http.core.plaintext,http.core.json `
  --protocol h3 `
  --load-tool h2load `
  --load-tool-mode docker `
  --connections 16 `
  --streams-per-connection 10 `
  --duration 10 `
  --warmup 2 `
  --repetitions 3 `
  --capture-counters `
  --counter-refresh-interval 1 `
  --output .artifacts\runs `
  --run-id local-h3-kestrel-counters-phase2k
```

`dotnet-counters` may be provided as a repo-local tool, a global tool, or an
explicit executable path through `--counter-tool`. The default is
`dotnet-counters`. `check` reports whether it is available.

ProtocolLab carries a local tool manifest with `dotnet-counters`. On a fresh
machine, restore it with:

```powershell
dotnet tool restore
```

When the tool is not on `PATH`, the runner tries the repo-local form:

```text
dotnet tool run dotnet-counters -- ...
```

If the tool is missing or collection fails, the benchmark cell can still run.
The result records `tool-unavailable`, `target-unresolved`, or `failed` and
preserves stdout/stderr and the attempted summary artifact. Counter failure does
not fabricate counter values and does not invalidate an otherwise valid
benchmark.

## Artifacts

Each counter-enabled cell uses stable artifact names:

- `diagnostic-target.json`
- `counters.stdout.txt`
- `counters.stderr.txt`
- `counters.raw.json`
- `counters.raw.csv`
- `counters-summary.json`

`result.json` links these artifacts through `counterArtifacts` and includes
`diagnosticTarget`, `countersAvailable`, `countersCaptureStatus`, and
`countersSummary`.

## Counter Summary

Parsing is best-effort. Raw counter output is authoritative.

The summary attempts to report:

- sample count and collection start/end timestamps
- CPU mean and max
- allocation rate mean or allocated bytes delta
- Gen0/Gen1/Gen2 collection deltas
- GC heap size mean/max
- GC pause time delta
- thread-pool thread count mean
- thread-pool queue length max
- exception count delta or exception rate mean
- parse warnings

Unavailable fields remain `null` and should be treated as missing evidence, not
zero.

## Interpretation

Runtime counters improve bottleneck direction by separating target CPU,
allocation/GC, thread-pool, and exception signals from load-tool throughput.
They do not change evidence class, comparability gates, load shape, or
publishability. Local shared-host h2load results remain
`external-reference-local`, even when counters are captured successfully.
