# ADR-0002: Execution Profiles

## Status

Proposed

## Context

ProtocolLab can run in many environments: a developer laptop with process-mode
targets, a Docker Desktop installation with containerized targets, a CI
pipeline with Docker-in-Docker, a remote process connected over SSH, and
(eventually) a dedicated lab with bare-metal servers and isolated networking.

Currently, the runner infers the execution environment from CLI arguments
(`--target-mode process|docker`, `--target-network-mode`) and records the
result in scattered `BenchmarkResult` fields (`DockerTargetMode`,
`DockerNetworkMode`, etc.). There is no single, typed value that represents
"where did this run execute."

The absence of a formal execution profile causes several problems:

- Consumers cannot easily filter results by environment type.
- Evidence classification must inspect multiple fields and flags to
  determine the environment context.
- It is possible to construct an incoherent configuration (e.g., Docker
  resource limits with process-mode target) that the runner does not
  validate.
- Future environments (CI containers, dedicated labs) have no defined
  vocabulary in the model.

## Decision

Introduce an `ExecutionProfile` enum to `Incursa.ProtocolLab.Model` with
these values:

- `LocalProcess` — runner and target on the same host, started as child
  processes. No container isolation.
- `LocalDockerBridge` — target runs in a Docker container with published-port
  or bridge networking. Runner is on the host.
- `LocalDockerHostNetwork` — target runs in a Docker container with host
  network mode. Runner is on the host.
- `RemoteProcess` — runner and target on separate hosts. Target is a process.
- `RemoteDocker` — runner and target on separate hosts. Target is a Docker
  container.
- `CiContainer` — runner runs inside a CI container (GitHub Actions, Jenkins
  agent). Docker availability depends on CI configuration.
- `DedicatedLabBareMetal` — controlled lab, bare-metal target, no container
  overhead. Known hardware, known OS, known network.
- `DedicatedLabContainer` — controlled lab, containerized target with known
  resource constraints and known container runtime configuration.

Rules:

1. The execution profile is determined once at the start of a run from the
   combination of CLI arguments, host detection, and (in the future)
   environment markers. It is recorded in `RunMetadata` and inherited by
   every cell.

2. The execution profile gates which collectors are available. For example,
   `dotnet-counters` is available for `LocalProcess` and `DedicatedLabBareMetal`
   but not for `CiContainer` unless `dotnet-counters` is installed in the CI
   image.

3. The execution profile is the primary input to evidence classification.
   `DedicatedLabBareMetal` with all collectors active produces
   `isolated-host` or `publishable` evidence. `LocalDockerBridge` with
   published-port networking produces `local-lab` or
   `external-reference-local` evidence.

4. Incoherent configurations (e.g., Docker resource limits with
   `LocalProcess` profile) must produce validation warnings or errors.

## Consequences

### Positive

- A single typed value represents the execution context, replacing scattered
  boolean and string fields.
- Evidence classification logic can branch on execution profile rather than
  inspecting multiple independent fields.
- Consumers can filter, group, and compare results by execution profile with
  a single field.
- New execution environments (CI, remote, lab) have a defined vocabulary
  before they are implemented.
- Profile-aware collector selection prevents asking for `docker-stats-target`
  during a `LocalProcess` run.

### Negative

- The CLI must be updated to accept or infer an execution profile.
- Existing `BenchmarkResult` Docker/process fields must either be replaced or
  kept in sync with the profile value.
- A profile enum with 8 values requires maintenance as new environments are
  added.

### Neutral

- The profile can initially be derived from existing CLI arguments
  (`--target-mode`, `--target-network-mode`) without changing the CLI surface.
- Existing evidence classification logic can continue to work alongside the
  new profile field until a migration is complete.

## Related

- [ADR-0001: Measurement Provenance](ADR-0001-measurement-provenance.md)
- [ADR-0003: Report Claim Levels](ADR-0003-report-claim-levels.md)
- [Measurement Model](../architecture/measurement-model.md)
- [Runner Model](../architecture/runner-model.md)
