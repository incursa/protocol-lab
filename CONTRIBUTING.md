# Contributing

Contributions are welcome when they preserve the public contract boundary and
keep ProtocolLab language-neutral.

## Before You Open A Pull Request

- Read `CONTRIBUTOR-AGREEMENT.md`.
- Follow `CODE_OF_CONDUCT.md`.
- Read `README.md`, `docs/README.md`, and
  `docs/protocol-lab/product-boundaries.md`.
- Keep changes focused on contracts, schemas, fixtures, scenarios, suites,
  load profiles, documentation, or governance files.
- Check that new normative requirements are authored in SpecTrace JSON and use
  stable requirement IDs.
- Check that Markdown support docs do not become the canonical source for a
  requirement, architecture decision, work item, or verification result.

## What To Include

- Contract changes should update the matching schema, fixture, documentation,
  and SpecTrace links.
- Fixture changes should include valid and invalid examples where the contract
  surface benefits from both.
- Boundary changes should update the public/internal boundary document and the
  migration notes when implementation-owned material moves out.

## What Not To Do

- Do not add executable source code, scripts, generated code, build files, or
  implementation automation to this repository.
- Do not add workflows except for public governance or repository-health
  validation.
- Do not present this repository as the canonical runner, command-line tool,
  SDK, hosted lab, package publisher, or implementation repository.
- Do not add private workspace paths, credentials, secrets, private service
  URLs, or private operational state.
- Do not use public docs to imply official certification, verified benchmark
  authority, or production hosted execution.

## Contributor Agreement

Pull requests are checked by the `Contributor Agreement` workflow. If the
workflow asks you to sign, read `CONTRIBUTOR-AGREEMENT.md` and comment exactly:

```text
I have read the Incursa Contributor Agreement and I hereby assign my contribution rights as described.
```

The workflow records signatures outside this repository. Maintainers should
follow `docs/contributor-agreement-automation.md` when configuring the required
secret or branch status check.

## Repository Health

The `Repository Health` workflow validates public repository hygiene only. It
parses JSON, checks local Markdown links, checks schema `$id` values, resolves
traceability paths, and fails if implementation folders or source/project
files are reintroduced.

## Style

- Use clear normative language in requirements: `MUST`, `MUST NOT`, `SHALL`,
  `SHALL NOT`, `SHOULD`, `SHOULD NOT`, or `MAY`.
- Keep public examples declarative and implementation-neutral.
- Prefer JSON Schema for JSON document contracts and OpenAPI/YAML for HTTP
  control-plane contracts.
