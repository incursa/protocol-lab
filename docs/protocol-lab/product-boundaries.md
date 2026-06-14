# ProtocolLab Product Boundaries

**Status:** Current. The public repository is a language-neutral contract
repository. Implementation repositories consume it.

## Overview

ProtocolLab has a public/implementation split:

- the public repository is the specification, schema, fixture, scenario,
  suite, load-profile, and documentation repository.
- internal, third-party, or product-specific implementation repositories own
  runtime behavior and operations.

The public repository defines stable contracts. Implementation repositories may
implement those contracts, operate hosted labs, retain operational artifacts,
and add private diagnostics. The public repository must not depend on
implementation code, implementation services, private configuration, private
credentials, or private deployment state.

## Layer Model

```text
+--------------------------------------------------+
| Internal / Implementation Layer                  |
| Hosted labs, concrete runners, package stores,   |
| worker orchestration, private diagnostics,       |
| retained operational artifacts                   |
+--------------------------------------------------+
| Public / Contract Layer                          |
| SpecTrace JSON, schemas, OpenAPI/YAML contracts, |
| fixtures, scenarios, suites, load profiles,      |
| public report and artifact contracts, docs       |
+--------------------------------------------------+
```

## Public Contract Layer

The public layer owns language-neutral definitions:

- requirements and traceability in SpecTrace JSON
- JSON Schema and OpenAPI/YAML contract files
- declarative fixtures for valid, invalid, and incompatible contract examples
- scenario and suite definitions
- load-profile definitions
- measurement, telemetry, artifact, redaction, comparability, and public-report
  contracts
- contribution, security, and governance documents

The public layer does not own local process execution, container execution,
continuous-integration automation, load-generator wrappers, local benchmarking,
retained run artifacts, package publication, object-store upload scripts, or a
canonical runner implementation.

## Internal And External Implementation Layer

Implementation repositories may provide:

- runners and command surfaces
- adapters and implementation packages
- test executors
- package materialization and worker orchestration
- hosted controller APIs and operational dashboards
- retained artifacts and attested provenance
- private diagnostics and release automation

Those implementations must consume the public contracts instead of redefining
them privately.

## Boundary Rules

1. Internal may consume public contracts.
2. Public must not consume internal code, services, configuration, or build
   outputs.
3. Public contracts must remain language-neutral.
4. Unsupported and unavailable states must remain explicit.
5. Raw QUIC and HTTP/3 lanes must not be collapsed into each other.
6. Public report contracts must not imply controlled, hosted, or publishable
   evidence unless the required provenance is present.
7. Concrete runner behavior belongs outside this public repository.

## Participation Model

A runner, adapter, implementation package, test executor, hosted controller,
report consumer, archive importer, or telemetry bundle participates by
satisfying public schemas and preserving public semantics. An
implementation-owned runner is one participant, not the source of truth.

Implementation-side telemetry is optional auxiliary evidence unless a run plan
explicitly requires it. The public layer defines normalized telemetry and
artifact manifest shapes, not the collector, telemetry backend, raw artifact
format, or storage implementation.

## Related Documents

- [README](../../README.md)
- [Package v2](../lab/package-v2.md)
- [Run Plan v1](../lab/run-plan-v1.md)
- [Test Case And Run Plan Model](../architecture/test-case-run-plan-model.md)
- [Measurement Model](../architecture/measurement-model.md)
- [SpecTrace Traceability](../../specs/traceability/README.md)
