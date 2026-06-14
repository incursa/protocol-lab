# Lab Roles

ProtocolLab roles describe contract participants. They do not require a
specific implementation repository or runtime.

## Public Contract Repository

The public repository defines schemas, fixtures, scenarios, suites, load
profiles, run plans, artifact contracts, report contracts, and SpecTrace
requirements.

## Runner Or Hosted Lab

A runner or hosted lab consumes public contracts, resolves package metadata,
executes selected work, and produces artifacts. It is an implementation of the
public contracts, not the public source of truth.

## Adapter

An adapter exposes an implementation-owned control plane for preparing,
starting, observing, and cleaning up a target.

## Test Executor

A test executor performs selected tests against prepared implementation
endpoints and reports metrics, artifacts, unsupported outcomes, and failures.

## Package Producer

A package producer publishes implementation, test-executor, scenario-pack, or
toolchain packages that satisfy the public package contract.

## Report Consumer

A report consumer validates public report schemas and preserves claim-level,
provenance, unsupported, and unavailable outcome semantics.
