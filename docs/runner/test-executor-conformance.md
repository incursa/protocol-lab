# ProtocolLab Test Executor Conformance

The test executor conformance suite proves that a control plane speaks the
ProtocolLab Test Executor Contract v1 before a controller or worker schedules
it for package-backed execution.

It is intentionally separate from the implementation adapter:

- the adapter prepares implementation endpoints
- the test executor runs the selected test against those endpoints
- the lab records package selection, compatibility, metrics, artifacts, and
  result classification

The suite is protocol-neutral. It sends opaque `testDocument` and
`scenarioDocument` JSON and verifies the executor HTTP lifecycle rather than
HTTP/3, QUIC, WebTransport, MASQUE, or any other protocol behavior.

## What the suite checks

- health and manifest discovery
- session creation and session lookup
- prepare, start, status, metrics, artifacts, stop, and delete
- unsupported test, scenario, or protocol handling
- problem-details responses
- malformed responses
- timeout and infrastructure failure classification
- delete idempotency

## How executors should be verified

Before a test-executor package is accepted by a controller or wired into a
worker, run the conformance suite against its control-plane URL from the
executor project or from a package fixture harness.

The suite can point at:

- a process-local loopback server
- a Docker-published port
- an external host
- a custom handler-backed test harness

The suite should pass for supported test/scenario/protocol selections and
should explicitly report unsupported, failed, malformed, timeout, and
infrastructure outcomes without collapsing them into one generic error class.

Package authors should also validate the package root manifest against
`schemas/package/v2/package.schema.json` and the packaged test-executor
manifest against `schemas/test-executor/v1/manifest.schema.json`. Passing the
conformance suite proves the control plane; passing the schema checks proves
the package/catalog metadata that schedulers consume.

## Local verification

Run the public CLI probe against a live Test Executor v1 control plane:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance test-executor --base-url <test-executor-control-plane-url> --test-id <supported-test-id> --scenario-id <supported-scenario-id> --scenario-version 1.0 --protocol <protocol-id>
```

The command calls health, manifest, session creation, session lookup, prepare,
start, status, metrics, artifacts, stop, and delete behavior using only public
Test Executor v1 schemas and models.

Run the conformance tests directly:

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~TestExecutorConformanceSuiteTests
```

Run the public schema checks directly:

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~PublicContractSchemaTests
```
