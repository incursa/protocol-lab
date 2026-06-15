# Governance Decisions

This page records small repository governance decisions that should stay
consistent across the ProtocolLab repository family.

## Notice File

`NOTICE.md` is not required for this repository at this time. This repository
currently has an Apache-2.0 license file, but no repo-local attribution text,
upstream NOTICE file, or third-party notice policy that requires a repository
NOTICE file. Add one only if future third-party notices, upstream NOTICE text,
or redistribution terms require it.

## Release Workflow

Release versioning follows SemVer and is based on the public contract/API
surface. Breaking public contract changes are major, additive compatible public
contract changes are minor, and compatible corrections are patch-level.

Official releases are created by signed or maintainer-controlled Git tags.
ProtocolLab remains the public contract repository and excludes package
publication, hosted lab operation, and implementation release automation, so no
release workflow is required until tagged contract snapshot automation is
deliberately introduced.
