# Architecture Overview

ProtocolLab separates public contracts from implementations.

## Public Contract Layer

The public layer defines:

- requirements, architecture, work items, and verification as SpecTrace JSON
- JSON Schemas for public document contracts
- OpenAPI/YAML contracts for HTTP control planes where applicable
- declarative fixtures for valid, invalid, and incompatible examples
- scenarios, suites, and load profiles
- measurement, telemetry, artifact, redaction, comparability, and public-report
  semantics

## Implementation Layer

Runners, hosted labs, adapters, implementation packages, test executors,
package stores, telemetry collectors, dashboards, and upload workflows are
implementation concerns. They consume public contracts and produce
contract-compliant artifacts outside this repository.

## Data Flow

```text
SpecTrace requirements
  -> schemas and control-plane contracts
  -> declarative fixtures, scenarios, suites, and load profiles
  -> implementation-owned runners/adapters/test executors
  -> telemetry and artifact contracts
  -> public-report contracts
  -> report consumers
```

The public repository owns the contract nodes in this flow. It does not own the
runtime execution nodes.

Measurement and telemetry contracts define normalized evidence that may be
returned after a run. Artifact contracts define hash-addressable manifests for
raw or derived evidence. Neither contract layer defines how evidence is
collected.
