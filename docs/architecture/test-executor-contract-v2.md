# Test Executor Prepare Contract v2

Test Executor Prepare v2 is the execution-ready handoff for a selected test
cell. It supplements, rather than mutates, the v1 management contract.

The request carries immutable snapshots for the run plan, test, scenario, and
resolved load profile. It also carries the exact scenario and load-profile
documents, target endpoints, artifact expectations, run and cell identities,
and an integrity policy that requires rejection when a canonical document
digest differs from its snapshot.

This removes repository lookup and precedence choices from executor behavior.
Scenario documents own protocol and workload semantics. Load-profile documents
own pressure, scheduling, repetitions, and outer operation timeouts. Executors
must not infer missing values from suite defaults or substitute another
protocol binding when preparation fails.

The machine-readable contract is
[`../../schemas/test-executor/v2/prepare-request.schema.json`](../../schemas/test-executor/v2/prepare-request.schema.json).
Complete valid and invalid examples are under
[`../../fixtures/public-contracts/test-executor/`](../../fixtures/public-contracts/test-executor/).
