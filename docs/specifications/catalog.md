---
title: "Specification Catalog"
---

# Specification Catalog

The initial catalog is a deliberately small contract pilot:

- [`quic-rfc9000-handshake-pilot`](../../specifications/catalogs/quic-rfc9000-handshake-pilot.json)
  contains two proposed RFC 9000 requirement records.
- [`quic.transport.handshake-cold`](../../specifications/scenario-mappings/quic.transport.handshake-cold.json)
  maps existing checks using only `exercises` and `observes` relationships.
- [`quic-handshake-bootstrap-pilot`](../../specifications/coverage-profiles/quic-handshake-bootstrap-pilot.json)
  defines the explicit pilot denominator and non-goals.

These records validate the contract and review workflow. They are not a
complete RFC 9000 mapping, a conformance profile, or a basis for implementation
ranking. New records should follow the
[mapping methodology](mapping-methodology.md) and remain proposed until their
source locators, roles, applicability, check isolation, evidence requirements,
and limitations have been reviewed.
