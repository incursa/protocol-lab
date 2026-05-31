# ProtocolLab Runner Contract Fixture Lab

The fixture lab proves the runner contract with fake manifests, fake scenarios, fake suites, fake validators, fake load tools, and fake target lifecycle outcomes.

It intentionally does not prove real protocol behavior. It does not require Docker, network access, or external services.

## Adapter Fixture Lab

The adapter contract is proven with a separate deterministic fixture harness under
`tests/Incursa.ProtocolLab.Tests/Fixtures/AdapterContractLab/`.

That fixture harness uses an in-memory ASP.NET Core test server to expose the
adapter control plane over HTTP/1.1 JSON while returning a fake protocol
endpoint description that can describe non-HTTP protocols such as `quic`.

It is used to prove:

- successful lifecycle sequencing
- structured `unsupported` results
- prepare, start, and readiness failure paths
- metrics and artifact discovery
- problem responses and malformed responses
- cleanup after stop and delete

The fixture harness does not perform benchmarking and does not require Docker.
It exists only to validate the adapter client and contract shapes.

Future real adapters should run the adapter conformance suite described in
[`docs/runner/adapter-conformance.md`](adapter-conformance.md) before the
runner is allowed to consume them.

The first real adapter, `Kestrel Adapter v1`, is documented in
[`docs/runner/kestrel-adapter.md`](kestrel-adapter.md). The fixture lab keeps
separate manifests for the real adapter control plane so runner tests can prove
the control-plane URL, returned protocol endpoint URL, metrics snapshots, and
artifact discovery without conflating them with the direct Kestrel target path.

## What It Proves

- successful cell execution
- unsupported and incompatible cells
- startup failure and readiness timeout
- validation pass, validation fail, validation unsupported, and fixture-only validation unavailable
- load success, load failure, and parse failure
- deterministic artifact layout
- machine-readable aggregate results
- human-readable summaries
- direct runner-host usage and CLI smoke usage
- structured runner events without console coupling

## What It Does Not Prove

- real HTTP/3, raw QUIC, WebTransport, or MASQUE behavior
- production protocol implementations
- Docker-dependent target execution
- publishable benchmark results

## Fixture Root

The fixture files live under:

`tests/Incursa.ProtocolLab.Tests/Fixtures/RunnerContractLab`

That directory contains:

- `implementations/` fake implementation manifests
- `scenarios/` fake scenarios and network profiles
- `load-tools/` fake load-tool manifests
- `suites/` the fixture suite document
- `scripts/` the dummy PowerShell target and load-tool scripts

## How To Run The Fixture Lab

Run the fixture tests:

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~RunnerContractFixtureLabTests
```

Run the CLI smoke path against the fixture root:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- run --root tests\Incursa.ProtocolLab.Tests\Fixtures\RunnerContractLab --implementations fixture-http-success --scenarios fixture.http.success --protocol h1 --load-tool fixture-load-success --output .artifacts\runs --run-id fixture-cli
```

Generate the fixture report:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- report --root tests\Incursa.ProtocolLab.Tests\Fixtures\RunnerContractLab --output .artifacts\runs --run-id fixture-cli
```

List fixture manifests:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- list implementations --root tests\Incursa.ProtocolLab.Tests\Fixtures\RunnerContractLab
dotnet run --project src\Incursa.ProtocolLab.Cli -- list scenarios --root tests\Incursa.ProtocolLab.Tests\Fixtures\RunnerContractLab
dotnet run --project src\Incursa.ProtocolLab.Cli -- list load-tools --root tests\Incursa.ProtocolLab.Tests\Fixtures\RunnerContractLab
```

## Where Artifacts Are Written

The runner writes fixture runs under:

`.artifacts/runs/{runId}`

Per-cell artifacts live under:

`.artifacts/runs/{runId}/implementations/{implementationId}/{scenarioId}/{protocol}/c{connections}-s{streams}-r{repetition}`

Key files:

- `result.json`
- `validation.json`
- `load-tool.stdout.txt`
- `load-tool.stderr.txt`
- `target.stdout.txt`
- `target.stderr.txt`
- `aggregate-results.json`
- `summary.md`

## How To Inspect Results

- `result.json` records the per-cell runner result.
- `validation.json` records validation status and diagnostics.
- `aggregate-results.json` records machine-readable aggregate output.
- `summary.md` records the human-readable report.
- `load-tool.stdout.txt` and `load-tool.stderr.txt` preserve raw load-tool output.
- `target.stdout.txt` and `target.stderr.txt` preserve target diagnostics.

## Fixture Failure Mapping

- unsupported implementation or scenario combinations map to compatibility filtering before target startup
- startup failure maps to failed target execution with preserved diagnostics
- readiness timeout maps to a failed validation result with target stdout/stderr preserved
- validation failure blocks accepted benchmark data
- parse failure keeps raw load-tool output while leaving parsed metrics unavailable
- fixture-only validation unavailable is represented by the fixture validator helper used in the tests
