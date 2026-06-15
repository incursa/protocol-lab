# ProtocolLab Artifact Requirements

This Markdown page is supporting documentation. The canonical authored artifact
requirements are the SpecTrace JSON artifacts in this directory.

## Scope

Raw artifacts MAY have any media type and MAY originate from any producer. The
public contract defines a safe manifest shape for referencing those artifacts,
not a storage backend, capture tool, upload workflow, or retention service.

## Redaction States

ProtocolLab artifact references use these redaction states:

- `not-reviewed`
- `producer-declared-safe`
- `sanitized`
- `contains-sensitive-data`
- `internal-only`
- `unknown`

## Normative Rules

- `PLAB-ARTIFACT-001`: Artifact references MUST include stable identity,
  media type, path, SHA-256 hash, size, creation time, producer, redaction
  state, sensitivity declaration, retention class, description, and tags.
- `PLAB-ARTIFACT-002`: Raw artifacts MUST be preserved by reference and
  content hash; the public contract MUST NOT require a specific file format,
  collector, storage service, or transport mechanism.
- `PLAB-ARTIFACT-003`: Public or sanitized artifacts MUST NOT declare that
  they contain sensitive data.
- `PLAB-ARTIFACT-004`: Artifact bundles MUST bind artifacts to the run, cell,
  scenario, protocol, implementation, test executor, load profile, and
  execution profile when those fields are known.
- `PLAB-ARTIFACT-005`: Artifact manifests MAY reference logs, traces, metrics,
  qlog files, packet captures, process output, profiles, summaries, reports,
  raw blobs, or other artifacts, but none of those kinds is required by the
  public contract.
- `PLAB-ARTIFACT-006`: Redaction state MUST be explicit before an artifact is
  used as public report evidence.
- `PLAB-ARTIFACT-007`: Private paths, credentials, secrets, hostnames, and
  implementation-only operational state SHOULD NOT enter public artifact
  bundles.
