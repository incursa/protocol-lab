# ProtocolLab Adapter Conformance

The adapter conformance suite proves that a control plane speaks the
ProtocolLab Adapter Contract v1 before the runner is allowed to consume it.

It is intentionally separate from the protocol endpoint under test:

- the runner talks to an adapter control plane over HTTP/1.1 JSON
- the adapter returns one or more protocol endpoints
- protocol traffic, validation, and load generation continue against those
  returned endpoints

The fixture adapter host under
`tests/Incursa.ProtocolLab.Tests/Fixtures/AdapterContractLab/` is the first
deterministic target used to prove the suite.

## What the suite checks

- health and manifest discovery
- session creation
- prepare, start, status, endpoint discovery, metrics, artifacts, stop, and
  delete
- unsupported scenario handling
- problem-details responses
- malformed responses
- timeout and infrastructure failure classification
- invalid lifecycle transitions
- delete idempotency

## How future adapters should be verified

Before an adapter is wired into the runner, run the conformance suite against
its control-plane URL from the adapter project or from a local fixture harness.

The suite can point at:

- a process-local loopback server
- a Docker-published port
- an external host
- a custom handler-backed test harness

The suite should pass for the adapter's supported scenarios and should
explicitly report unsupported, failed, malformed, timeout, and infrastructure
outcomes without collapsing them into one generic error class.

## Local verification

Run the conformance tests directly:

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~AdapterConformanceSuiteTests
```

Run the schema checks directly:

```powershell
dotnet test Incursa.ProtocolLab.sln --no-build --filter FullyQualifiedName~AdapterSchemaValidatorTests
```

