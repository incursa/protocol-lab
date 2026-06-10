# ProtocolLab Test Executor Contract v1

**Status:** Contract v1

The Test Executor Contract defines the HTTP management API for packages that run tests against prepared implementation endpoints. It is separate from the Adapter Contract:

- An adapter owns implementation lifecycle and returns target endpoints.
- A test executor owns traffic generation, conformance checks, performance checks, capability checks, metrics, and executor artifacts.
- ProtocolLab owns package selection, compatibility planning, worker placement, artifact collection, and result classification.

The executor control plane is HTTP/1.1 JSON and is rooted at:

```text
/protocol-lab/test-executor/v1
```

## Routes

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/health` | Report executor process health and contract compatibility. |
| `GET` | `/manifest` | Advertise supported protocols, tests, scenarios, endpoint requirements, metrics, and artifacts. |
| `POST` | `/sessions` | Create an executor session for one planned cell. |
| `GET` | `/sessions/{sessionId}` | Return session state. |
| `POST` | `/sessions/{sessionId}/prepare` | Bind the selected test/scenario and target endpoints. |
| `POST` | `/sessions/{sessionId}/start` | Start execution. |
| `GET` | `/sessions/{sessionId}/status` | Return executor state and operation details. |
| `GET` | `/sessions/{sessionId}/metrics` | Return executor metrics snapshot. |
| `GET` | `/sessions/{sessionId}/artifacts` | Return executor artifact snapshot. |
| `POST` | `/sessions/{sessionId}/stop` | Stop an active session. |
| `DELETE` | `/sessions/{sessionId}` | Dispose session state. |

Schemas live under `schemas/test-executor/v1`.

The canonical contract-version string in health and manifest
`versionCompatibility` payloads is `test-executor-v1`. The route segment is
`v1`, but package manifests and conformance checks should use the full
contract identifier.

## Manifest

The manifest identifies the executor and declares:

- `supportedTestSelectors`: tests the executor can run.
- `supportedScenarioSelectors`: scenarios the executor can exercise.
- `supportedProtocolFamilies`: protocol families such as `http`, `h2`, `h3`, `quic`, `webtransport`, or `masque`.
- `requiredTargetEndpointBindings`: target endpoints that must be supplied during prepare.
- `claimedCapabilities`: executor capabilities and limitations.
- `metricsAvailability` and `supportedArtifactTypes`.

The manifest is descriptive, not authoritative scheduling policy. Controllers and workers still validate selected packages, scenarios, and worker capabilities before execution.

## IDs And Selectors

`testId` identifies the executor-owned check being run. `scenarioId`
identifies the ProtocolLab scenario document being exercised. They are
separate fields because one executor can expose tests that map to one
scenario, many scenarios, or a narrower check inside a scenario.

Public IDs should be stable ASCII tokens that match the package v2 token
shape: start with a letter or digit and then use only letters, digits,
underscore, period, colon, or hyphen.

`supportedTestSelectors` must use selector metadata such as `test-id` to
describe test matching. `supportedScenarioSelectors` must use selector
metadata such as `scenario-id` to describe scenario matching. Wildcards,
prefixes, tags, or custom expressions are allowed only as explicit metadata
that both the executor and lab understand. A worker must not infer support from
the executable name, implementation brand, package name, or protocol family.

## Prepare

`prepare` receives:

- `testId`, `scenarioId`, `scenarioVersion`, and `protocol`.
- Opaque `testDocument` and `scenarioDocument` JSON.
- `targetEndpoints` returned by an adapter or resolved by the lab.
- `runId`, `cellId`, and artifact expectations.

Executors must return `unsupported` when they understand the request but cannot run that test/scenario/protocol combination. They should return `application/problem+json` only for malformed requests or operational failures.

## Boundary Rules

- Test executors must not start implementation targets directly unless the package also provides a separate adapter and the lab explicitly selects it.
- Workers must not substitute a different executor or protocol lane when a selected executor is unavailable or incompatible.
- Raw QUIC remains limited to the currently validated scenario cells until additional executor validation gates are added.
- Executor metrics and artifacts are evidence inputs. They do not make a result publishable unless ProtocolLab validation and classification accept the cell.

## Conformance

The reusable conformance surface lives in
`Incursa.ProtocolLab.Adapter.Conformance` as `TestExecutorConformanceSuite`.
It exercises health, manifest, session lifecycle, prepare/start/status,
metrics, artifacts, stop, delete, unsupported results, problem-details,
malformed responses, timeouts, and infrastructure failures.

Package authors should run the suite against the executor control-plane URL
before publishing or submitting a test-executor package.

See [`docs/runner/test-executor-conformance.md`](../runner/test-executor-conformance.md)
for the focused verification workflow.
