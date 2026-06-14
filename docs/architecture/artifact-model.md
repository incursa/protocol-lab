# Artifact Model

ProtocolLab artifacts are durable evidence references produced by a runner,
implementation, adapter, test executor, load generator, collector, or
environment. The public repository defines manifest semantics and schemas, not
the runtime collector, directory writer, storage service, or upload workflow.

## Public Requirements

Artifact references preserve:

- run identity
- selected package, implementation, test-executor, scenario, suite, and
  load-profile metadata
- artifact kind and media type
- path or optional URI
- SHA-256 content hash
- size and creation time
- producer identity
- redaction state and sensitivity declaration
- retention class, description, and tags

Raw artifacts are preserved by reference and content hash. Normalized
measurement bundles carry reportable summaries and samples; artifact bundles
describe the raw or derived evidence that may support those summaries.

Public report bundles must reference only artifacts that are safe to publish.
Artifacts marked public-safe or sanitized must not also declare that they
contain sensitive data. Private paths, credentials, hostnames, and internal
operational state should not enter public artifacts.
