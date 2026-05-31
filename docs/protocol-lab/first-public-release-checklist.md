# First Public Release Checklist

This checklist tracks the first public release cut for ProtocolLab. The goal
is a professional public repo that stays conservative about evidence and does
not imply official, verified, or hosted benchmark authority.

## Repository Surface

- [x] README explains what ProtocolLab is and is not.
- [x] README lists current supported scenarios.
- [x] README shows build, validation, and basic benchmark commands.
- [x] README points to validation-vs-benchmarking and product boundaries.

## Policy And Governance

- [x] LICENSE is present and correct.
- [x] SECURITY.md is present.
- [x] CONTRIBUTING.md is present.
- [x] Issue templates are present.
- [x] Pull request template is present.
- [ ] CODE_OF_CONDUCT.md has been added if the maintainers want one.

## Docs And Boundaries

- [x] docs/README.md links the public release checklist and validation-vs-benchmarking.
- [x] docs/quickstart.md has a clean path for new users.
- [x] docs/protocol-lab/vision.md avoids official or verified overclaims.
- [x] docs/protocol-lab/product-boundaries.md makes the public/community versus internal/commercial split explicit.
- [x] docs/protocol-lab/roadmap.md stays framed as a living roadmap, not a promise.

## Build And Verification

- [x] dotnet restore, build, and test pass.
- [x] Validation CLI examples work.
- [x] Basic benchmark example is documented.
- [x] Shared NuGet packages are defined for downstream consumers.
- [x] Package publishing workflow exists for tagged releases.
- [x] Link check passes.
- [x] Leak scan passes.
- [x] workbench validate --profile core passes in the local Codex/Workbench environment.

## Release Gate

- [ ] The repository tree is clean.
- [x] No blocked docs links remain.
- [x] No public files contain private workspace paths or secrets.
- [ ] The repo is ready for an initial public tag.

## Remaining Polish

- Optional: add CODE_OF_CONDUCT.md before or after the first public tag.
- Optional: add badges or release notes once the tag exists.
