# Contributing

Contributions are welcome when they keep the public/community boundary clear
and stay aligned with the repository's evidence model.

## Before You Open A Pull Request

- Read [CONTRIBUTOR-AGREEMENT.md](CONTRIBUTOR-AGREEMENT.md) and sign the
  agreement on your pull request if you are not on the workflow allowlist.
- Read [README.md](README.md), [docs/README.md](docs/README.md), and
  [docs/protocol-lab/product-boundaries.md](docs/protocol-lab/product-boundaries.md).
- Run `dotnet tool restore`.
- Run `dotnet restore Incursa.ProtocolLab.sln`.
- Run `dotnet build Incursa.ProtocolLab.sln --no-restore`.
- Run `dotnet test Incursa.ProtocolLab.sln --no-build`.
- If you are using the Codex/Workbench environment, run
  `workbench validate --profile core`.
- If your change touches public docs or templates, run the markdown link and
  leak checks used by CI.

## What To Include

- Behavior changes should include focused tests.
- Documentation changes should update the relevant public entrypoints and
  release checklist when the public surface changes.
- Benchmark-related changes should state the evidence class and measurement
  limitations clearly.
- Public/private boundary changes should update the boundary document and, when
  needed, the first-public-release checklist.

## What Not To Do

- Do not claim official certification, verified benchmark authority, or
  production-grade hosted execution without explicit support in the repo's
  evidence model.
- Do not introduce private workspace paths, credentials, or secrets into the
  public tree.
- Do not hide commercial or hosted assumptions in the public repo.

## Style

- Keep changes focused and reviewable.
- Prefer repository-local scripts and commands over ad hoc instructions.
- Update docs when public-facing behavior changes.
