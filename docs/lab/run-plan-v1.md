# Run Plan v1

Run plan v1 is the public contract for repeatable package-backed ProtocolLab
jobs. It pins exact package bytes and selects existing package-provided
components. It is not a scenario language, test definition format, executor
extension point, or controller execution API.

The canonical JSON Schema is
`schemas/run-plan/v1/run-plan.schema.json`.

## Contract Version

Every v1 run plan uses:

```json
{
  "schemaVersion": "protocol-lab-run-plan-v1"
}
```

The schema version identifies the public run-plan payload shape. Package
manifests keep using `protocol-lab-package-v2`; test executors keep using
`test-executor-v1`.

## Required Shape

A run plan must include:

- `runPlanId` and `runPlanVersion`
- `packages`, where every entry pins `packageId`, `packageVersion`, and
  `sha256`
- `implementationIds`
- `testExecutorIds`
- `suiteIds` or `scenarioIds`
- `protocols`
- `loadProfileId`
- `targetMode`
- `targetNetworkMode`
- `requiredCapabilities`

The selected IDs must refer to entries provided by the pinned packages. The run
plan itself does not define or override scenario behavior, tests, load shapes,
executor flags, endpoint details, or implementation behavior.

Validate a run plan with the public conformance command after resolving the
referenced package archives or package roots:

```powershell
dotnet run --project src\Incursa.ProtocolLab.Cli -- conformance run-plan `
  --run-plan fixtures\public-contracts\run-plans\valid\http1-conformance-smoke-reference.json `
  --package fixtures\public-contracts\packages\http1-core-scenario-pack `
  --package fixtures\public-contracts\packages\reference-http1-test-executor `
  --package fixtures\public-contracts\packages\reference-http1-implementation
```

Controllers should call the same validator, or the equivalent
`RunPlanConformanceValidator`, before previewing or creating a job. A plan that
fails schema validation, references a package not supplied to the validator, or
selects IDs not provided by the resolved packages must be rejected before job
creation.

## Package References

Package references are immutable provenance anchors:

```json
{
  "packageId": "protocol-lab-h3-core-scenarios",
  "packageVersion": "2026.06.10",
  "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
}
```

Controllers and workers must resolve the selected components from exactly
those package identities and SHA-256 values. They must not silently substitute
another package, implementation, test executor, suite, scenario, protocol, or
load profile.

## Selection Fields

`implementationIds`, `testExecutorIds`, `protocols`, and either `suiteIds` or
`scenarioIds` are explicit selector lists. A controller may expand suites into
their package-defined scenarios, but it must not infer missing selectors from
names or compatibility guesses.

`loadProfileId` selects an existing load profile. It does not inline load
settings. `targetMode` is one of `process`, `docker`, or `external`.
`targetNetworkMode` is one of `published-port` or `shared-docker-network`.

`requiredCapabilities` lists worker capabilities the run needs:

```json
[
  {
    "name": "protocol-lab-cli",
    "value": "true"
  }
]
```

An empty list is valid when the selected packages do not require additional
worker capabilities.

## Optional Metadata

Run plans may include `displayName`, `repetitions`, `comparisonGroups`,
`publicationIntent`, `labels`, `traceReferences`, and `notes`. These fields
describe execution policy or provenance. They do not create new scenario or
test behavior.

Use suite package metadata and labels to carry result classification through
controllers, workers, artifacts, and the site. The public HTTP/1 fixtures use:

- `conformance-smoke` with `purpose: conformance`,
  `resultKind: conformance`, and `labels: ["result-kind:conformance"]`
- `benchmark-smoke` with `purpose: benchmark`, `resultKind: benchmark`, and
  `labels: ["result-kind:benchmark"]`

Conformance answers whether behavior is valid. Benchmark answers how
performance looks under a load profile. A slow valid run is not a conformance
failure, and a fast invalid run is still invalid. Site importers should
distinguish these surfaces from suite/run-plan metadata, not by guessing from
metrics.

## Unsupported And Unavailable Outcomes

Run plans select intended work. They do not turn unsupported or unavailable
package combinations into runnable cells. If the selected package set cannot
provide a requested implementation, test executor, suite, scenario, protocol,
endpoint binding, load shape, or worker capability, the controller should
reject the plan during preview or the worker should emit an explicit
unsupported or unavailable result with a reason.

Unsupported means a selected package understands the request shape but does
not claim the requested behavior. Unavailable means a required package,
capability, dependency, collector, endpoint, or worker resource is missing at
execution time. Neither outcome authorizes fallback to another implementation,
executor, protocol lane, scenario, package version, or load profile.

Public examples live under `fixtures/public-contracts/run-plans/valid` and
`fixtures/public-contracts/run-plans/invalid`.

## Example

```json
{
  "schemaVersion": "protocol-lab-run-plan-v1",
  "runPlanId": "h3-core-smoke-reference",
  "runPlanVersion": "2026.06.10",
  "displayName": "HTTP/3 core smoke reference run",
  "packages": [
    {
      "packageId": "protocol-lab-h3-core-scenarios",
      "packageVersion": "2026.06.10",
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
    },
    {
      "packageId": "protocol-lab-managed-h3-test-executor",
      "packageVersion": "2026.06.10",
      "sha256": "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"
    },
    {
      "packageId": "kestrel-http3",
      "packageVersion": "2026.06.10",
      "sha256": "1111111111111111111111111111111111111111111111111111111111111111"
    }
  ],
  "suiteIds": ["h3-local-v1"],
  "implementationIds": ["kestrel-http3"],
  "testExecutorIds": ["managed-httpclient-h3-load"],
  "protocols": ["h3"],
  "loadProfileId": "smoke",
  "targetMode": "process",
  "targetNetworkMode": "published-port",
  "requiredCapabilities": [
    {
      "name": "protocol-lab-cli",
      "value": "true"
    }
  ],
  "comparisonGroups": [
    {
      "groupId": "h3-core",
      "suiteIds": ["h3-local-v1"],
      "sameExecutorRequired": true,
      "sameLoadProfileRequired": true
    }
  ],
  "publicationIntent": "local-only",
  "notes": "Reference package-backed smoke run."
}
```

## Non-Goals

Run plan v1 does not wire controller execution in this repository. It defines
the public payload that controllers can later accept as an upload or job body.

Run plan v1 does not define:

- scenario YAML
- test-executor manifests
- package manifests
- load profile contents
- implementation endpoint details
- private executor flags
- report claim levels stronger than the collected evidence
