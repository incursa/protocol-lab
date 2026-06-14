# Contributor Agreement Automation

This repository uses the Incursa-owned contributor agreement action:

```text
incursa/contributor-agreement-action@v0.1.1
```

The action checks pull request contributors, comments with the required signing
instructions, records signatures in a private repository, and publishes the
`Contributor Agreement` commit status that branch rulesets can require.

This workflow follows the current Incursa pattern used by `codex-dotnet`,
`codex-telegram`, and `codex-telegram-goal-command`.

## Storage

Signatures are stored outside this repository in the private repository:

```text
incursa/contributor-agreements
```

The shared signature file is:

```text
signatures/incursa-contributor-agreement-v1.json
```

Do not create that file manually. The action creates it on the first
signature.

## Required Secret

The workflow expects this organization or repository secret:

```text
INCURSA_CONTRIBUTOR_AGREEMENTS_TOKEN
```

Use a fine-grained token scoped only to the signature storage repository.
Recommended token permissions are Contents read/write and Metadata read.
Store the token as an Actions secret, not a variable.

Owner actions:

1. Confirm the organization secret `INCURSA_CONTRIBUTOR_AGREEMENTS_TOKEN` is
   visible to `incursa/protocol-lab`. If the secret uses selected repository
   access, add this repository to the selected list.
2. Confirm repository Actions policy permits these actions:
   `incursa/contributor-agreement-action@v0.1.1` and `actions/checkout@v4`.
3. Open a test pull request from a non-allowlisted account.
4. Sign with the exact comment phrase.
5. Confirm the signature is written to `incursa/contributor-agreements`.
6. Require the `Contributor Agreement` status check only after the workflow has
   run successfully on a pull request.

## GitHub Ruleset Setup

Use a repository ruleset for the default branch. The intended ruleset is:

```text
Name: Protocol Lab Pull Request Gate
Target: Branch
Enforcement: Active
Branch target: Default branch
Bypass: Organization administrators only, if Incursa policy allows admin bypass
```

Required rules:

- Require a pull request before merging.
- Require at least one approving review.
- Require review thread resolution before merging.
- Require review from Code Owners when the pull request changes owned files.
- Require status checks to pass before merging.
- Block force pushes.
- Block branch deletion.

Required status checks:

```text
Contributor Agreement
Public Contract Repository Health
```

`Contributor Agreement` is the commit status published by
`incursa/contributor-agreement-action@v0.1.1`. `Public Contract Repository
Health` is the job name from `.github/workflows/validate.yml`.

Do not require package, build, release, .NET, Docker, Node, Python, or runner
checks for this repository. This repository is a public specification and
contract repository; implementation build and release checks belong in
implementation repositories.

Do not enable required signed commits unless the maintainer signing path is
known to work reliably for this repository. Signed commits are separate from
the contributor agreement gate and are not required by the CLA workflow.

Recommended optional settings:

- Keep stale review dismissal disabled unless Incursa wants every new push to
  invalidate prior approvals.
- Keep "require branches to be up to date before merging" disabled unless the
  repository starts seeing status-check races.
- Keep merge, squash, and rebase merge methods allowed unless Incursa adopts a
  repository-wide merge-method policy.

## Pull Request Flow

The workflow runs on `pull_request_target` and selected pull request comments.
It does not check out or execute pull request code.

If a contributor has not signed, the action comments with the contributor
agreement link and the exact phrase to post:

```text
I have read the Incursa Contributor Agreement and I hereby assign my contribution rights as described.
```

To re-run the check manually, comment:

```text
recheck contributor agreement
```

The action implementation lives in `incursa/contributor-agreement-action`, so
future automation changes should usually be made there instead of copying logic
into this repository.
