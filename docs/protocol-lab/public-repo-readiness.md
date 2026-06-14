# Public Repository Readiness

**Status:** Repositioned as a public contract repository.

The public repository is ready when it can be understood without any local
runner, build system, package publisher, or hosted lab implementation.

## Readiness Criteria

- The root README presents ProtocolLab as a language-neutral specification and
  contract repository.
- Public contracts are represented by SpecTrace JSON, JSON Schema,
  OpenAPI/YAML where applicable, fixtures, scenarios, suites, load profiles,
  and support documentation.
- Implementation code, scripts, build files, and workflow automation are not
  present.
- Public docs do not instruct contributors to execute repository-local runner,
  benchmark, validation, publication, or upload workflows.
- The internal/public dependency direction is explicit: implementation
  repositories may consume public contracts; public contracts do not consume
  implementation repositories.

## Preserved Contract Assets

- `schemas/`
- `fixtures/public-contracts/`
- `scenarios/`
- `suites/`
- `load-profiles/`
- `specs/`
- root governance files

## Known Follow-Up Risk

Downstream implementation repositories need their own validation and admission
checks because this public repository no longer carries executable validators.
Those checks should consume the public schemas and fixtures directly.
