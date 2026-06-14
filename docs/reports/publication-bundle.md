# Public Report Bundle Contract

Public report bundles are contract-defined derivatives of completed
measurement work. The public repository defines the expected shape and safety
rules; implementations decide how bundles are produced and where they are
stored.

The bundle contract is anchored by the schemas under
`schemas/public-report/v1/`.

## Report Vocabulary

- An evidence report is the schema-versioned payload that preserves claim
  level, provenance, selected contract metadata, and evidence references.
- A public report is the publication-safe report surface derived from evidence
  that is allowed to be shared.
- A publication bundle is the collection of evidence reports, report indexes,
  publication manifests, and public-safe artifact indexes prepared for a
  consumer.
- A publication handoff is the consumer-facing transfer boundary for a
  publication bundle. It is not a separate payload family.
- Public report safety is the rule set that prevents private paths, secrets,
  internal hostnames, non-public artifacts, and inflated claims from entering
  a public report or bundle.

## Report Schema Surfaces

- `evidence-report-v1.schema.json` defines the shareable run evidence payload.
- `publication-manifest.schema.json` defines publication bundle identity,
  report inventory, and handoff metadata.
- `report-index.schema.json` defines an index of public report entries.
- `report-index-entry.schema.json` defines one report index entry.
- `artifacts-index.schema.json` defines the public-safe artifact index for
  report consumers.

## Required Semantics

A public report bundle must:

- preserve the run identity used by the source evidence
- preserve claim-level and publishability fields without upgrading them
- identify the scenarios, suites, implementations, test executors, and load
  profiles involved
- include artifact references only when the referenced artifacts are safe for
  public consumption
- keep diagnostic-only reports labeled as diagnostic-only
- avoid private paths, secrets, internal hostnames, and private service state

## Non-Canonical Status

Publication bundles are not the source of truth for ProtocolLab contracts.
They are consumer-facing report artifacts that must conform to public schemas
and preserve provenance supplied by an implementation.
