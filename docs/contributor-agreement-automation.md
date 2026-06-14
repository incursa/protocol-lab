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

1. Confirm this repository can read `INCURSA_CONTRIBUTOR_AGREEMENTS_TOKEN`.
2. Open a test pull request from a non-allowlisted account.
3. Sign with the exact comment phrase.
4. Confirm the signature is written to `incursa/contributor-agreements`.
5. Require the `Contributor Agreement` status check only after the workflow has
   run successfully on the protected branch.

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
