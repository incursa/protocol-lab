# Test Case And Run Plan Model

**Status:** Proposed architecture and vocabulary lock. This document defines
the intended public ProtocolLab grammar for specs, scenarios, scenario packs,
test executors, implementation packages, load profiles, and repeatable run
plans. Existing schema names still use `scenario`; this document explains how
that term relates to a test case.

## Purpose

ProtocolLab needs to avoid a sequencing trap:

1. An implementation package is hard to write without knowing what tests and
   executor behavior it must satisfy.
2. A test executor is hard to validate without an implementation endpoint to
   exercise.
3. A controller job is hard to repeat without a file that pins package
   versions, selected tests, selected implementations, and load shape.

The resolution is to make the public test definition the first-class design
input. A test definition must be detailed enough that an executor author can
write a tester from it and an implementation author can expose an endpoint for
it without sharing source code, runtime language, or implementation-specific
assumptions.

## Decision Summary

ProtocolLab uses this ordering for new protocol lanes:

1. **Specify the test case.** Define the protocol behavior, endpoint binding,
   input payload, expected observations, validation rules, artifact
   expectations, and supported load-shape semantics.
2. **Implement one or more test executors.** Each executor declares exactly
   which test case IDs, scenario IDs, protocols, endpoint bindings, metrics,
   and artifacts it supports.
3. **Implement one or more implementation packages.** Each package declares
   which implementation IDs, protocols, roles, scenarios, and capabilities it
   supports. Partial support is normal and must be explicit.
4. **Create a run plan.** The run plan pins package identities and SHA-256
   values, then selects implementations, test executors, scenarios, protocols,
   load profile, and execution policy for repeatable jobs.

This means a new lane can start with an intentionally boring reference
implementation package. That package exists to prove the test-case
specification, executor, controller, worker, and report path. It is not the
benchmark target the project ultimately cares about.

## Vocabulary

| Term | Meaning | Owns | Must not own |
| --- | --- | --- | --- |
| Test case | One implementation-neutral behavior or workload specification. | Wire behavior, request/response or stream shape, expected observations, validation requirements. | Package versions, implementation selection, executor selection, host names, ports, duration, concurrency. |
| Scenario | The current ProtocolLab catalog artifact that serializes one test case. | Stable ID, protocol, role, requirements, traffic shape, protocol-specific body, validation and artifact expectations. | Implementation names, executor names, package versions, machine assumptions. |
| Scenario pack | A versioned package that provides scenarios and/or suites. | Scenario files, suite files, package metadata for provided scenario and suite IDs. | Test executor binaries, implementation binaries, hidden fallback policy. |
| Suite | A named grouping of scenarios for a protocol lane or workflow. | Scenario selection, allowed test executors, default protocol and local defaults. | Package SHA pins, implementation versions, claim upgrades. |
| Load profile | A named intensity profile. | Connections, streams/concurrency, duration, warmup, repetitions, purpose. | What behavior is tested. |
| Test executor package | A runnable tester/validator/load generator package. | Traffic generation, validation execution, metrics, executor artifacts, unsupported outcomes. | Starting implementation targets directly, substituting another protocol lane. |
| Implementation package | A runnable target under test. | Implementation identity, adapter/control plane, endpoint lifecycle, supported protocols/scenarios/capabilities, implementation artifacts. | Test selection policy, executor behavior, benchmark claims. |
| Adapter | The control plane for an implementation package. | Start, prepare, endpoint discovery, status, metrics, artifacts, cleanup. | Protocol traffic semantics of the test executor. |
| Toolchain package | Optional support dependency package. | Shared worker prerequisites and capability requirements. | Provided scenarios, provided implementations, provided test executors. |
| Run plan | A repeatable job manifest. | Exact package references, selected IDs, load profile, execution policy, comparison grouping. | New behavior definitions or stronger report claims. |

For public file and API compatibility, the repository continues to use
`scenario` as the YAML artifact name. When speaking about the behavior being
specified, `test case` is the conceptual term. A simple scenario normally
contains exactly one test case.

## Layering Model

```text
Test case specification
  -> serialized as scenario YAML
  -> grouped by scenario-pack and suite
  -> exercised by selected test executor package
  -> run against selected implementation package
  -> shaped by selected load profile
  -> pinned by selected run plan
  -> reported as evidence report cells
```

The same test case can be used for validation, benchmarking, regression, and
diagnostics. The load profile changes intensity. The test executor changes
how traffic is generated and how observations are collected. The
implementation package changes the target. The test case remains the same.

## Test Case Specification

A test case specification is the implementation-neutral contract that makes
executor-first development possible. It must answer these questions:

- What role is the implementation playing: `server`, `client`, `proxy`,
  `relay`, `observer`, or another declared role?
- What protocol family is being exercised: `h1`, `h2`, `h3`, `quic`,
  `webtransport`, `masque`, or a future canonical ID?
- What endpoint binding is required from the implementation adapter?
- What traffic shape is required: request/response, upload, download,
  streaming, bidirectional stream, datagram, handshake-only, or connection
  lifecycle?
- What bytes, frames, messages, or semantic inputs does the executor send?
- Which parts of the input are fixed, parameterized, randomized, echoed,
  hashed, or otherwise generated?
- What must the executor observe for the test case to pass?
- What failures are validation failures, unsupported outcomes, executor
  failures, implementation failures, or infrastructure failures?
- What artifacts must be preserved?
- What metrics may be emitted, and which are required for benchmark
  acceptance?
- Which load shape fields can be applied without changing the meaning of the
  test case?

The current scenario YAML schema is the first serialization of that
specification. If future test cases need multi-step scripts, richer payload
generators, or protocol-frame grammars, those should extend the scenario
schema rather than create a separate package kind.

## Scenario Semantics

A scenario is one durable public test-case artifact. It should be named by
behavior, not by implementation or executor.

Good IDs:

- `http.core.plaintext`
- `http.core.json`
- `http.payload.bytes.1kb`
- `http.upload.echo.64kb`
- `quic.transport.duplex-streams`
- `webtransport.session.open`

Bad IDs:

- `kestrel.plaintext`
- `incursa-http3-json`
- `h2load-large-body`
- `docker-nginx-smoke`

Scenario fields should describe:

- **identity:** `id`, `title`, `version`, `status`, `kind`
- **classification:** `family`, `layer`, `protocol`, `roles`
- **requirements:** required protocols, roles, capabilities
- **endpoint or protocol body:** HTTP endpoint, QUIC transport action, H3
  protocol behavior, WebTransport session, MASQUE tunnel, or future body
- **traffic shape:** the high-level traffic pattern
- **validation:** required checks and expected observations
- **benchmark compatibility:** load shapes and metrics that make sense for
  this behavior
- **artifacts:** required and optional evidence
- **comparability:** notes or requirements that affect report grouping

Scenario files do not declare package versions, implementation package IDs,
test executor package IDs, concrete ports, local paths, Docker settings, host
machine labels, or controller scheduling policy.

## HTTP Plaintext Example

The existing `http.core.plaintext` scenario is a simple test case:

- role: server
- protocol lane: HTTP application over `h1`, `h2`, or `h3`
- endpoint binding: an HTTP server endpoint discovered from the selected
  implementation adapter or manifest
- request: `GET /plaintext`
- expected status: `200`
- expected content type: `text/plain`
- expected body: `Hello, World!`
- traffic shape: request/response
- required validation: status, content type, body
- benchmark-compatible metrics: request rate, latency percentiles, error rate

If the project later wants randomized plaintext echo, that should be a
different scenario such as `http.echo.plaintext-random`. It would define:

- how the random payload is generated
- the valid payload size range
- whether the executor sends the payload in the request body, query, header,
  stream, or datagram
- whether the target must echo exactly, echo normalized text, return a hash,
  or return a protocol-specific acknowledgement
- which random seed or generated payload artifact must be preserved so the
  run can be reproduced

That distinction matters. `http.core.plaintext` is a fixed response test.
`http.echo.plaintext-random` is an echo semantics test. They are separate test
cases even if both use plaintext bytes.

## Scenario Pack Semantics

A scenario pack packages a set of scenarios and optionally suites. It is the
versioned distribution unit for public or private test-case definitions.

A scenario pack should be small enough to version coherently. Prefer these
kinds of packs:

- `protocol-lab-http-core-scenarios`
- `protocol-lab-http-payload-scenarios`
- `protocol-lab-h3-protocol-scenarios`
- `protocol-lab-raw-quic-transport-scenarios`
- `protocol-lab-webtransport-session-scenarios`

Avoid mega-packs that mix unrelated protocols, unrelated maturity levels, or
experimental and stable surfaces that will version at different speeds.

Scenario packs declare what they provide through package v2 metadata:

- `providedScenarios`
- `providedSuites`
- supported `protocols`
- suite-compatible `testExecutors`

They do not carry test executor binaries unless they are also a separate
`test-executor` package. Cross-kind packages make compatibility ambiguous and
should be avoided.

## Suite Semantics

A suite is a convenient named selection of scenarios for one workflow. It is
not the repeatable job manifest.

A suite may declare:

- selected scenario IDs
- default protocol
- compatible test executor IDs
- local defaults used when a run plan does not override them
- notes explaining the intended workflow

A suite must not pin:

- package SHA-256 values
- implementation package versions
- executor package versions
- controller node placement
- report publication claims

Example suite purposes:

- `http1-core-smoke`: fast HTTP/1 request/response validation
- `http2-core-smoke`: fast HTTP/2 request/response validation
- `h3-local-v1`: local HTTP/3 acceptance coverage
- `quic-transport-v1-comparison`: raw QUIC transport comparison coverage

## Load Profile Semantics

A load profile changes intensity, not behavior. It should be idempotent with
respect to the test case. Running `http.core.plaintext` with `smoke` and
running it with `local-regression` still exercises the same test case; the
number of connections, duration, repetitions, and streams differ.

Load profiles own:

- duration
- warmup
- repetitions
- connection count
- streams or concurrency
- purpose such as smoke, comparison, regression, stress, soak, or publishable
  benchmark

Load profiles do not own:

- request path
- expected response
- payload generator semantics
- target implementation selection
- executor language or executable
- package references

Executors may interpret load-profile fields differently. The runner must
record requested load shape, effective load shape, and warnings when a field
is ignored, derived, constrained, or unsupported.

## Test Executor Package Semantics

A test executor package provides code that exercises selected test cases
against prepared endpoints. It can be implemented in C#, Go, Rust, Python,
Node, or any other runtime.

The executor manifest must declare:

- executor identity and version compatibility
- supported test selectors
- supported scenario selectors
- supported protocol families
- supported execution modes
- required target endpoint bindings
- claimed capabilities and limitations
- supported artifact types
- metrics availability
- package-local process or control-plane launch metadata when used by the
  worker

Multiple executors may support the same scenario:

- a C# HTTP executor
- a Go HTTP executor
- a Rust HTTP executor
- `h2load`
- `oha`
- a protocol-specific conformance executor

That is valid. The report must treat executor identity, executor version,
parser, and evidence class as part of the comparison context. Results from
different executors may be displayed together for diagnostics, but they must
not be ranked as equivalent benchmark evidence unless the report model
explicitly proves comparability.

Executors must return explicit unsupported outcomes when they understand the
request but cannot run the selected test case, protocol, endpoint binding,
load shape, or artifact mode. Unsupported is not an infrastructure failure.

Executors must not:

- start implementation targets directly unless a selected package also
  provides an adapter and the run plan explicitly selects that relationship
- infer support from implementation brand, executable name, language runtime,
  or package name
- fall back from one protocol lane to another
- fabricate metrics when parsing fails

## Implementation Package Semantics

An implementation package provides a runnable target under test. It may carry
an adapter control plane, target binaries, scripts, manifests, certificates,
and implementation-specific artifact exporters.

Implementation packages should usually be scoped to a protocol lane when the
underlying product spans many protocols. Early packages should prefer:

- `kestrel-http1`
- `kestrel-http2`
- `kestrel-http3`
- `caddy-http1`
- `caddy-http2`
- `caddy-http3`
- `quic-go-http3`

over a single broad `kestrel` or `caddy` package that claims many protocols.
A broader package can be introduced later when the project has proven the
compatibility model across all included lanes.

The package v2 manifest should declare only the subset that is intended to
run:

- `providedImplementations[].implementationId`
- `providedImplementations[].protocols`
- `providedImplementations[].scenarios`
- package dependencies and required capabilities
- package environments and entrypoints

The adapter manifest should further declare:

- implementation identity
- supported roles
- claimed capabilities
- supported scenario selectors
- supported endpoint types
- metrics availability
- supported artifact types

Partial implementation support is normal. A package that supports QUIC does
not automatically support every QUIC transport test case. It should declare
the scenario IDs or selector metadata it supports. A controller, worker, or
runner that receives an unsupported scenario must produce an explicit
unsupported outcome or reject the incompatible package set before execution.

## Unsupported And Partial Support

ProtocolLab distinguishes these outcomes:

| Outcome | Meaning | Example |
| --- | --- | --- |
| Unsupported | The package understands the request shape but does not claim this scenario, protocol, endpoint binding, capability, or load shape. | A QUIC implementation does not support datagrams. |
| Missing capability | The selected implementation or worker lacks a declared required capability. | `libmsquic` is required but unavailable. |
| Validation failed | The implementation claimed support but did not satisfy the test-case observations. | HTTP body differs from expected body. |
| Executor failed | The test executor crashed or returned an operational failure. | Executor process exits non-zero before producing result artifacts. |
| Infrastructure failed | The lab could not prepare or run the job independent of target correctness. | Package SHA mismatch or worker cannot start Docker. |
| Parser failed | Raw executor output exists, but automated metric extraction failed. | `h2load` output format is unrecognized. |

Unsupported is not a bad package by itself. It is only a problem when a run
plan expected that package to support the selected test case. In that case the
job preview should block submission, or the run should produce an explicit
unsupported cell with a reason.

Support should be declared at multiple layers:

- scenario requirements say what the test case needs
- scenario-pack metadata says which scenarios and suites the package provides
- suite metadata says which executors are allowed
- test-executor metadata says which tests, scenarios, protocols, endpoint
  bindings, and worker capabilities it supports
- implementation metadata says which protocols and scenarios it supports
- adapter metadata says which capabilities, endpoint types, and scenario
  selectors the live adapter supports
- worker metadata says which machine capabilities are available

The lab must intersect these declarations. It must not infer compatibility
from names.

## Run Plan Semantics

A run plan is the repeatable job manifest. It binds stable definitions to
exact package bytes and selected execution policy.

A run plan should be accepted by the controller as a first-class upload or as
the body of a job submission. It should be safe to upload the same run plan
later and get the same selected package versions, scenario IDs, executor IDs,
implementation IDs, protocols, and load profiles. Environment-dependent
measurements may still vary, but the intended work is identical.

A run plan owns:

- run plan ID and version
- exact package references: `packageId`, `packageVersion`, `sha256`
- selected implementation IDs
- selected test executor IDs
- selected scenario IDs or suite IDs
- selected protocol IDs
- selected load profile ID
- execution mode and target network mode
- required worker capabilities
- repetitions or other allowed overrides
- comparison grouping rules
- publication intent or local-only intent
- optional labels, notes, and trace references

A run plan does not own:

- new test-case behavior
- hidden package fallback
- stronger claim levels than the evidence supports
- implementation-specific endpoint details that should come from adapters
- executor-specific private flags that are not part of the executor contract

### Draft Run Plan Shape

```json
{
  "schemaVersion": "protocol-lab-run-plan-v1",
  "runPlanId": "http1-core-smoke-reference",
  "runPlanVersion": "2026.06.10",
  "displayName": "HTTP/1 core smoke reference run",
  "packages": [
    {
      "packageId": "protocol-lab-http-core-scenarios",
      "packageVersion": "2026.06.10",
      "sha256": "..."
    },
    {
      "packageId": "protocol-lab-csharp-http-smoke-executor",
      "packageVersion": "2026.06.10",
      "sha256": "..."
    },
    {
      "packageId": "kestrel-http1",
      "packageVersion": "2026.06.10",
      "sha256": "..."
    }
  ],
  "suiteIds": [
    "http1-core-smoke"
  ],
  "scenarioIds": [
    "http.core.plaintext",
    "http.core.json"
  ],
  "implementationIds": [
    "kestrel-http1"
  ],
  "testExecutorIds": [
    "protocol-lab-csharp-http-smoke-executor"
  ],
  "protocols": [
    "h1"
  ],
  "loadProfileId": "smoke",
  "targetMode": "process",
  "targetNetworkMode": "published-port",
  "requiredCapabilities": [
    {
      "name": "protocol-lab-cli",
      "value": "true"
    }
  ],
  "comparisonGroups": [
    {
      "groupId": "http1-core",
      "scenarioIds": [
        "http.core.plaintext",
        "http.core.json"
      ],
      "sameExecutorRequired": true,
      "sameLoadProfileRequired": true
    }
  ],
  "notes": "Reference smoke run for proving HTTP/1 package-backed orchestration."
}
```

The exact schema can evolve, but the run plan must remain a selector and
provenance document. It should not become a second scenario language.

## Spec-First Development Flow

New test lanes should follow this flow:

1. **Write or update the test-case specification.** Add a scenario YAML file
   and, when needed, a supporting architecture/spec document that explains
   behavior not obvious from the YAML.
2. **Mark maturity honestly.** Use `draft`, `experimental`, or `placeholder`
   until a validator and executor exist.
3. **Write executor conformance tests.** Prove the executor advertises the
   test case, accepts valid prepare requests, returns unsupported for
   unsupported requests, emits required artifacts, and reports metrics only
   when available.
4. **Build a boring reference implementation package.** It may be Kestrel,
   a tiny Go server, a Rust server, or another simple target. Its purpose is
   to validate the test spec and executor path.
5. **Run the reference vertical slice.** Use a run plan that pins the scenario
   pack, executor package, and reference implementation package.
6. **Add comparison implementations.** Add Caddy, nginx, quic-go, Incursa, or
   other packages only after the test case and executor behavior are stable
   enough to classify results.
7. **Promote scenario maturity.** Move from draft or experimental to stable
   only when validation, artifacts, unsupported handling, and reporting are
   proven.

This flow allows executor-first work without blocking on a production
implementation, while still forcing the executor to prove itself against a
real endpoint.

## Technology Agnosticism

All package kinds must be technology-agnostic at the ProtocolLab boundary.
The package may contain C#, Go, Rust, C, C++, Python, shell scripts, Docker
images, or static binaries. The lab only relies on:

- package manifest metadata
- package-relative paths
- declared environment and worker capabilities
- adapter control-plane contract
- test executor contract
- scenario and suite catalog metadata
- raw artifacts and normalized result JSON

The controller and worker should be able to materialize package bytes, select
an environment entrypoint, start the package, wait for readiness, run selected
tests, collect artifacts, and classify the outcome without knowing the
language or framework inside the package.

## Invariants

These rules must remain true:

- A scenario/test case is implementation-neutral.
- A scenario pack provides scenarios and suites, not runnable test code.
- A test executor package provides runnable test code, not target
  implementation behavior.
- An implementation package provides the target under test, not the test
  selection policy.
- A load profile changes intensity, not behavior.
- A run plan pins and selects existing artifacts; it does not define new
  behavior.
- Package-backed jobs must use explicit package inventory and selected IDs.
- The lab must not silently substitute another implementation, executor,
  scenario, protocol, package version, or load profile.
- Unsupported outcomes must be explicit and attributable.
- Validation must pass before benchmark metrics are accepted.
- Raw stdout, stderr, command line, package provenance, and materialized
  package identity must be preserved.

## Initial Roadmap

### Step 1: Vocabulary And Docs

- Adopt this document as the public vocabulary reference.
- Update scenario authoring guidance to mention test cases and run plans.
- Rename internal UI text from "support package" to "toolchain package" when
  practical.

### Step 2: HTTP/1 Core Slice

- Add or refine `http.core.*` scenario specs so they are protocol-neutral
  across `h1`, `h2`, and `h3` where appropriate.
- Create `http1-core-smoke` suite.
- Create a small HTTP smoke executor package.
- Create `kestrel-http1` or another boring reference implementation package.
- Create a run plan that pins all three.

### Step 3: HTTP/2 Core Slice

- Reuse HTTP core scenarios where semantics match.
- Add HTTP/2-specific protocol validation scenarios only when the behavior is
  actually HTTP/2-specific.
- Create `http2-core-smoke` suite and reference implementation package.

### Step 4: Executor Multiplicity

- Add a second executor for the same HTTP core scenarios, preferably in a
  different runtime.
- Prove reports keep executor identity and comparability visible.

### Step 5: Wider Implementation Matrix

- Add lane-scoped packages for Caddy, nginx, quic-go, and other existing
  implementations.
- Keep broad product packages deferred until lane-scoped package behavior is
  boring.

### Step 6: Richer Protocol Families

- Promote H3 protocol scenarios after H3-specific validators and executors
  exist.
- Keep raw QUIC narrow until transport validators and artifact gates are
  strong.
- Keep WebTransport, MASQUE, and other future families explicit unsupported
  or placeholder until scenario, executor, and implementation packages all
  exist.

## Acceptance Criteria

This model is ready for implementation when all of these can be demonstrated:

- A contributor can read one scenario/test-case spec and write a test executor
  without reading an implementation package.
- A contributor can read the same spec and write an implementation adapter
  without reading executor source.
- A package can declare partial support without being considered invalid.
- A controller inventory view can show which selected packages provide the
  requested implementation, executor, scenario, suite, protocol, and worker
  capabilities.
- A job preview can block incompatible package sets before execution.
- A run plan can be uploaded twice and select the same packages and IDs both
  times.
- A report cell records scenario ID, test executor ID, implementation ID,
  protocol, load profile, package provenance, validation status, benchmark
  status, artifacts, and unsupported reasons when applicable.

## Related Documents

- [Scenario Model](scenario-model.md)
- [Runner Model](runner-model.md)
- [Adapter Model](adapter-model.md)
- [Test Executor Contract v1](test-executor-contract-v1.md)
- [Load Model](load-model.md)
- [Package v2](../lab/package-v2.md)
- [Scenario Authoring Guide](../scenarios/authoring-guide.md)
