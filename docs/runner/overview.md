# ProtocolLab Runner Overview

The ProtocolLab runner is the implementation-neutral orchestration layer for manifests, run planning, compatibility filtering, target lifecycle, validation, load execution, artifact capture, and reporting.

It is not a protocol implementation host. Protocol-specific behavior stays behind implementation manifests, validators, load tools, and execution backends.

Adapter control-plane details are documented separately in
[`../architecture/adapter-contract-v1.md`](../architecture/adapter-contract-v1.md).
The runner uses that contract to control a separate protocol endpoint rather
than talking to the endpoint directly through the adapter service.

The fixture lab in [fixture-lab.md](fixture-lab.md) demonstrates the runner contract with dummy components only.

For the repository-wide ownership map, see [../repository-layout.md](../repository-layout.md).
