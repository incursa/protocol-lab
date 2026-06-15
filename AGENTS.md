# ProtocolLab Agent Instructions

This is the public ProtocolLab specification and contract repository. Public
contracts authored here are the source of truth; internal runners, hosted
labs, packages, adapters, test executors, and implementation repositories must
conform to these contracts.

## Authority

Follow this order when working in the repository:

1. Canonical SpecTrace JSON artifacts under [`specs/`](specs/).
2. JSON Schema and OpenAPI/YAML contracts under [`schemas/`](schemas/).
3. Declarative fixtures under [`fixtures/public-contracts/`](fixtures/public-contracts/).
4. Scenario, suite, and load-profile definitions under [`scenarios/`](scenarios/), [`suites/`](suites/),
   and [`load-profiles/`](load-profiles/).
5. Markdown documentation and governance files.

Markdown docs are support and navigation unless a document explicitly states
otherwise. Do not make Markdown the only source of truth for a normative
requirement that belongs in SpecTrace JSON.

## Boundary Rules

- Do not add implementation code, runner code, CLI code, SDKs, generated code,
  repo-local scripts, source projects, or implementation logic.
- Do not introduce dependencies on .NET, Docker, OpenTelemetry, NuGet, Bash,
  Python, PowerShell, Node, Go, or any specific runner as public contract
  requirements.
- Workflows are allowed only for public governance and repository-health
  validation.
- JSON Schemas and OpenAPI/YAML define machine-readable public contracts.
- Fixtures must remain declarative, language-neutral examples. They must not
  contain executable entrypoints, scripts, binaries, private paths, or runtime
  assumptions.
- If internal behavior disagrees with public contracts, document the drift or
  update the public contract only when the public contract is genuinely wrong.
  Do not weaken public contracts to match older internal behavior.

## Change Routing

- Contract changes should update the matching SpecTrace requirement,
  architecture, verification, schema, fixture, and documentation surfaces where
  applicable.
- Schema changes should include valid and invalid fixtures when the contract
  surface benefits from examples.
- Public/internal boundary changes should update
  [`docs/protocol-lab/product-boundaries.md`](docs/protocol-lab/product-boundaries.md)
  and affected public indexes.
- Governance changes may update root governance files, `.github/`, and docs
  indexes, but must not reintroduce implementation build or publication
  workflows.

## Repository-Health Checks

Before finishing changes, run or perform the available repository-health
checks:

- JSON parse for all `*.json`.
- Markdown relative-link scan for repository-local links.
- `git diff --check`.
- Forbidden implementation folder and source-extension scan.
- Schema `$id` consistency scan for
  `https://schemas.incursa.com/protocol-lab/`.
- Traceability path resolution for [`specs/traceability/trace-links.json`](specs/traceability/trace-links.json).

Use the `.github/workflows/validate.yml` workflow as the committed definition
of repository-health validation. If a local environment lacks one of these
checks, document the gap in the final response.
