# ADR-0004: Public / Private Repository Split

## Status

Proposed

## Context

ProtocolLab currently lives in a private repository that contains both
public-candidate material and internal-only material. The current tree already
has a public seed, but it also includes content that should not be published as
is:

- analysis and work-item artifacts
- generated benchmark outputs
- private sample dependencies
- internal scripts and planning notes

The product boundary docs already describe a conceptual public-canonical layer
and a conceptual private/internal layer. What is missing is a concrete
repository split decision that makes those roles operational.

The split must satisfy these constraints:

- The public repository must be useful on its own.
- The private repository must explicitly carry the `-internal` suffix.
- Internal may depend on public.
- Public must not depend on internal.
- Private git history must not be published as-is.

## Decision

ProtocolLab will be split into two repositories:

1. `Incursa.ProtocolLab` as the public canonical repository.
2. `Incursa.ProtocolLab.Internal` as the private internal repository.

The split will be implemented as a clean repository boundary, not as a hidden
runtime mode.

The public repository will own the community-facing surfaces:

- community contracts
- schemas
- adapter SDK and adapter conformance assets
- report schema and artifact schema/layout
- basic local runner and CLI
- basic scenario catalog
- sample reports and examples
- public docs and contribution guidance

The internal repository will own the internal-only surfaces:

- extended scenarios
- hosted execution
- dashboards
- retained artifact workflows
- private CI integrations
- private adapters
- commercial diagnostics
- internal infrastructure and scripts
- unreleased experimental work

Shared contract surfaces such as the adapter contract, schema set, and public
model types must remain public and versioned so that the internal repository can
consume them without creating reverse dependencies.

The initial public seed will be created from public-safe files only. Incursa
HTTP/3 remains cleanup-gated and does not enter the initial public seed until
its private sample dependency is removed or replaced.

## Consequences

### Positive

- The public repository can be published as a coherent community project.
- The internal repository can keep hosted, retained, and private workflows
  without polluting the public surface.
- Shared contract surfaces become easier to version and audit.
- The repo boundary becomes explicit enough for drift checks and package
  boundaries.

### Negative

- The split adds coordination overhead for shared contracts.
- Some current project and test surfaces will need cleanup before publication.
- The internal repo will need to consume public packages or a sibling public
  checkout during the transition.
- Private history cannot be reused directly as public history without
  additional filtering or a fresh public repository.

### Neutral

- Existing project names and namespaces are not changed by this decision.
- The decision does not require immediate runtime behavior changes.
- The decision does not imply that every current repository file is public-safe;
  the inventory and split plan still govern the file-level selection.

## Related

- [Public / Private Repository Candidate Inventory](../protocol-lab/public-private-inventory.md)
- [Repository Split Plan](../protocol-lab/repository-split-plan.md)
- [Product Boundaries](../protocol-lab/product-boundaries.md)
- [Architecture Decision Records](README.md)
- [Adapter Contract v1](../architecture/adapter-contract-v1.md)
- [Artifact Model](../architecture/artifact-model.md)
- [Measurement Model](../architecture/measurement-model.md)
- [Report Model](../architecture/report-model.md)

