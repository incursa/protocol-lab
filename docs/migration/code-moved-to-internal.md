# Code Moved To Internal

This public repository is now a language-neutral specification, contract,
schema, fixture, scenario, suite, load-profile, and documentation repository.
Executable implementation assets belong outside the public tree.

During the public repository split, implementation-owned assets were moved to
or retained in a sibling internal implementation repository. This public
repository records only the categories that no longer belong here; it does not
duplicate implementation code or private operational details.

## Removed Public Path Categories

- workflow automation and build metadata
- implementation source trees and test projects
- server and tool implementations
- runner, command-surface, validation-harness, and package-materialization
  assets
- container and toolchain configuration
- local benchmarking and hosted-lab operation documentation
- implementation-specific SpecTrace artifacts
- executable fixture payloads

## Ownership After Migration

Runner implementations, command surfaces, validation harnesses, package
materialization, benchmarking, hosted lab operations, publication automation,
runtime collectors, provider integrations, and executable test tools now belong
in implementation repositories.

The public repository keeps only the contracts those implementations consume.
