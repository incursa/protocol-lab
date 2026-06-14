# Scenario Model

A scenario serializes one public test case. It is implementation-neutral and
does not select packages, runners, adapters, or test executors.

## Scenario Owns

- stable scenario ID
- protocol lane
- behavior description
- request or transport shape
- validation expectations
- artifact expectations
- status and tags

## Scenario Does Not Own

- package references
- implementation IDs
- test-executor IDs
- hostnames or ports
- private controller policy
- fallback behavior
- runtime-specific validation logic

## Status

- `stable`: public contract is expected to remain compatible.
- `experimental`: contract may change.
- `placeholder`: future scenario; no implementation support is implied.

## Relationship To Other Documents

Suites group scenarios. Load profiles describe intensity. Run plans select
package-pinned components and scenarios or suites. Reports describe evidence
produced by implementations.
