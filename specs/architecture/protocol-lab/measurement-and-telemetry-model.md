# Measurement And Telemetry Model

This Markdown page is a support note for the canonical SpecTrace architecture
artifact `ARC-PL-MEASUREMENT-TELEMETRY`.

## Model

ProtocolLab separates four evidence layers:

1. Scenario validation decides whether the selected behavior was valid,
   unsupported, unavailable, or failed.
2. Normalized measurement bundles describe reportable observations such as
   latency, throughput, errors, resource summaries, load shape, and collector
   provenance.
3. Artifact bundles describe raw or derived evidence by manifest, media type,
   hash, redaction state, and producer.
4. Public reports select safe evidence and state the claim level the run can
   support.

The model is intentionally language-neutral. Implementations can collect
telemetry through any internal mechanism, or through no implementation-side
telemetry at all. The public contract accepts normalized samples, summaries,
collector descriptors, artifact references, warnings, and errors.

## Timing Authority

For benchmark claims, runner-observed request timing is the canonical timing
evidence unless a specific test-executor contract explicitly assigns that
authority elsewhere. Implementation telemetry can explain behavior, but it does
not rewrite benchmark timing or conformance status after the run.

## Optional Export

Adapters and test executors may advertise optional telemetry export capability.
A runner may request post-run telemetry, but the participant may return no
telemetry. Export failure is diagnostic unless the run plan explicitly requires
that export. A telemetry bundle affects evidence quality, comparability, and
diagnostic value; it does not change conformance pass/fail status after the
fact.

## Comparability

Comparability is an evidence statement. The public vocabulary is `none`,
`same-run`, `same-profile`, `same-environment`, `lab-controlled`, and
`public-reference`. It is not a numeric score and does not create a stronger
claim than the profile, environment, and provenance can support.
