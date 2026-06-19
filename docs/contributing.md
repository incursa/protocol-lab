# Contributing To ProtocolLab Docs And Contracts

ProtocolLab contributions should preserve the public contract boundary:
contracts here are language-neutral and implementation repositories consume
them.

## Documentation Changes

Documentation should help readers understand the existing public contracts.
When documentation describes a normative rule, check whether the rule already
exists in SpecTrace JSON. If it does not, add or update the appropriate
SpecTrace artifact instead of making Markdown the only source of truth.

Documentation under `docs/` is mirrored to the central Incursa documentation
repository. Treat this repository as authoritative and the central docs copy
as generated publication staging.

## Specification Changes

Specification changes should update the matching surfaces:

- SpecTrace requirements, architecture, work items, or verification records
- JSON Schemas or control-plane payload contracts
- valid, invalid, or incompatible fixtures where examples clarify behavior
- scenario, suite, or load-profile definitions when selected work changes
- supporting documentation and indexes
- traceability links when requirements connect to schemas, fixtures, or
  verification

## Tests And Validation

This repository does not contain implementation tests. Its validation surface
is repository health: JSON and YAML parseability, schema consistency, fixture
validity, Markdown links, traceability path resolution, and
public/implementation boundary checks.

Implementation behavior, runtime diagnostics, benchmark execution, and hosted
lab proof belong in implementation repositories. When an implementation
reveals a public contract gap, update ProtocolLab contracts here and keep the
runtime fix in the implementation repository.

## Pull Request Checklist

Before opening a pull request:

- read the root contribution and contributor-agreement docs
- keep implementation code, source projects, local launchers, and generated
  outputs out of this repository
- keep public examples declarative and implementation-neutral
- update SpecTrace artifacts before relying on Markdown for normative
  requirement changes
- include schema and fixture updates when a contract shape changes
- mention any docs that should be mirrored into the central docs repository
