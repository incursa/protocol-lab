# Security Policy

ProtocolLab is a public specification, schema, fixture, and contract
repository. Report suspected security issues privately.

## Reporting

- Do not open a public issue or PR for a suspected vulnerability.
- Use GitHub private vulnerability reporting if it is enabled for this
  repository.
- If GitHub private vulnerability reporting is unavailable, contact
  `security@incursa.com`.
- For general open-source or governance questions, contact `oss@incursa.com`.

Please include:

- affected file, commit, or branch
- reproduction steps
- observed impact
- whether the issue exposes secrets, private paths, credentials, or unsafe
  defaults

## Scope

The public repository does not run a hosted service and does not contain
runtime tooling. Security concerns here are usually about leaked paths,
credentials, unsafe defaults, unsafe publication guidance, or docs that
overreach the public boundary.

## Response

We will triage privately and coordinate remediation before any public
disclosure when possible.
