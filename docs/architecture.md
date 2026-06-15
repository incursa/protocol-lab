# Architecture

This summary describes the public contract architecture. Detailed supporting
pages live under [`docs/architecture/`](architecture/).

## Contract Ownership

The public repository owns language-neutral contracts:

- SpecTrace JSON requirements, architecture, work items, and verification
- JSON Schemas and control-plane contract files
- declarative fixtures
- scenarios, suites, and load profiles
- measurement, artifact, and report semantics

## Implementation Ownership

Implementation repositories own concrete runners, adapters, test executors,
package materialization, hosted lab operations, diagnostics, publication
automation, and retained runtime artifacts.

## Boundary

Implementation repositories may consume public contracts. The public
repository must not depend on implementation code, internal services, private
configuration, or private artifacts.
