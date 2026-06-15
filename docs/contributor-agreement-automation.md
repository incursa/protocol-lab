# Contributor Agreement Automation

ProtocolLab uses the Incursa contributor agreement workflow to gate pull
requests from non-allowlisted contributors. The agreement text is
[`../CONTRIBUTOR-AGREEMENT.md`](../CONTRIBUTOR-AGREEMENT.md), and the workflow
definition is
[`../.github/workflows/contributor-agreement.yml`](../.github/workflows/contributor-agreement.yml).

## Workflow

- Workflow name: `Contributor Agreement`
- Job name: `Agreement Gate`
- Required status check name: `Contributor Agreement`
- Action: `incursa/contributor-agreement-action@v0.1.1`
- Storage owner: `incursa`
- Storage repository: `contributor-agreements`
- Storage branch: `main`
- Storage path: `signatures/incursa-contributor-agreement-v1.json`
- Agreement ID: `incursa-contributor-agreement-v1`
- Agreement URL:
  `${{ github.server_url }}/${{ github.repository }}/blob/main/CONTRIBUTOR-AGREEMENT.md`
- Recheck comment: `recheck contributor agreement`

The signing phrase must remain exactly aligned in
[`../CONTRIBUTING.md`](../CONTRIBUTING.md),
[`../CONTRIBUTOR-AGREEMENT.md`](../CONTRIBUTOR-AGREEMENT.md),
[`../.github/PULL_REQUEST_TEMPLATE.md`](../.github/PULL_REQUEST_TEMPLATE.md),
and
[`../.github/workflows/contributor-agreement.yml`](../.github/workflows/contributor-agreement.yml):

```text
I have read the Incursa Contributor Agreement and I hereby assign my contribution rights as described.
```

## Trigger And Permissions

The workflow runs on `pull_request_target` for opened, reopened, synchronized,
and ready-for-review pull requests. It also runs on `issue_comment` when the
comment is on a pull request and the comment body exactly matches either the
signing phrase or the recheck comment.

The workflow does not check out pull request code. Its permissions are limited
to:

- `contents: read`
- `issues: write`
- `pull-requests: read`
- `statuses: write`

## Secret

`INCURSA_CONTRIBUTOR_AGREEMENTS_TOKEN` is expected to be available from the
Incursa organization secret set. The token is used only to update the signature
storage repository. Use a narrowly scoped token that can read and write the
storage repository and read metadata.

Do not store the token in this repository, in workflow files, in documentation
examples, or as a GitHub Actions variable.

## Allowlist

The workflow allowlist is defined in
[`../.github/workflows/contributor-agreement.yml`](../.github/workflows/contributor-agreement.yml).
Changing it is a maintainer decision because allowlisted contributors bypass
the recorded signature check.

## Maintainer Setup

1. Confirm this repository can read the organization-level
   `INCURSA_CONTRIBUTOR_AGREEMENTS_TOKEN` secret.
2. Open or use a non-allowlisted test pull request.
3. Confirm the workflow asks for the exact signing phrase.
4. Add the signing phrase as a pull request comment.
5. Confirm the signature is recorded in
   `incursa/contributor-agreements` at
   `signatures/incursa-contributor-agreement-v1.json`.
6. Confirm the `Contributor Agreement` status check passes.
7. Require the `Contributor Agreement` status check in branch rules only after
   it has run successfully on the protected branch.

## Branch Rules

After the workflow exists on the protected branch and has run at least once,
require the `Contributor Agreement` status check through the repository branch
ruleset together with the repo validation check and CODEOWNERS review.
