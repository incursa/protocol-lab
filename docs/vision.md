# Vision

ProtocolLab provides language-neutral contracts for protocol measurement and
reporting. The public repository defines what participants must exchange and
preserve; implementation repositories decide how to execute those contracts.

## Questions The Public Contracts Answer

- What behavior does a scenario define?
- Which scenarios belong in a suite?
- What load shape does a load profile describe?
- Which packages, implementations, test executors, and scenarios does a run
  plan select?
- What artifacts and report fields are required to interpret evidence?
- Which outcomes are unsupported, unavailable, invalid, diagnostic, benchmark,
  or verified?

## Principles

- Contracts are language-neutral.
- Unsupported and unavailable states remain explicit.
- Raw QUIC transport and managed HTTP/3 lanes stay separate.
- Public reports preserve claim boundaries.
- Implementation code and runnable automation belong outside the public
  repository.
