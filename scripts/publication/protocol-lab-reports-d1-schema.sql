PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS public_report_runs (
  run_id TEXT PRIMARY KEY,
  generated_at TEXT NOT NULL,
  published_at TEXT NOT NULL,
  claim_level TEXT NOT NULL,
  publishable INTEGER NOT NULL,
  diagnostic_only INTEGER NOT NULL,
  execution_profile TEXT NOT NULL,
  visibility TEXT NOT NULL,
  source_kind TEXT NOT NULL,
  evidence_warning_count INTEGER NOT NULL,
  publication_warning_count INTEGER NOT NULL,
  copied_artifact_count INTEGER NOT NULL,
  skipped_artifact_count INTEGER NOT NULL,
  implementation_count INTEGER NOT NULL,
  scenario_count INTEGER NOT NULL,
  protocol_count INTEGER NOT NULL,
  validation_passed INTEGER NOT NULL,
  validation_failed INTEGER NOT NULL,
  validation_unsupported INTEGER NOT NULL,
  validation_not_applicable INTEGER NOT NULL,
  validation_inconclusive INTEGER NOT NULL,
  validation_infrastructure_failure INTEGER NOT NULL,
  benchmark_accepted INTEGER NOT NULL,
  benchmark_rejected INTEGER NOT NULL,
  benchmark_not_run_validation_failed INTEGER NOT NULL,
  benchmark_not_run_unsupported INTEGER NOT NULL,
  benchmark_not_run_load_tool_failed INTEGER NOT NULL,
  benchmark_not_run_parser_failed INTEGER NOT NULL,
  artifact_root_key TEXT NOT NULL,
  bundle_prefix TEXT NOT NULL,
  evidence_report_json_key TEXT NOT NULL,
  evidence_report_markdown_key TEXT NOT NULL,
  artifacts_index_key TEXT NOT NULL,
  publication_manifest_key TEXT NOT NULL,
  publication_warnings_key TEXT NOT NULL,
  publication_skipped_key TEXT NOT NULL,
  report_index_entry_key TEXT NOT NULL,
  report_index_key TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_public_report_runs_generated_at ON public_report_runs(generated_at DESC);
CREATE INDEX IF NOT EXISTS idx_public_report_runs_claim_level ON public_report_runs(claim_level);
CREATE INDEX IF NOT EXISTS idx_public_report_runs_publishable ON public_report_runs(publishable);
CREATE INDEX IF NOT EXISTS idx_public_report_runs_execution_profile ON public_report_runs(execution_profile);

CREATE TABLE IF NOT EXISTS public_report_run_implementations (
  run_id TEXT NOT NULL,
  implementation_id TEXT NOT NULL,
  PRIMARY KEY (run_id, implementation_id)
);

CREATE INDEX IF NOT EXISTS idx_public_report_run_implementations_implementation_id ON public_report_run_implementations(implementation_id);

CREATE TABLE IF NOT EXISTS public_report_run_scenarios (
  run_id TEXT NOT NULL,
  scenario_id TEXT NOT NULL,
  PRIMARY KEY (run_id, scenario_id)
);

CREATE INDEX IF NOT EXISTS idx_public_report_run_scenarios_scenario_id ON public_report_run_scenarios(scenario_id);

CREATE TABLE IF NOT EXISTS public_report_run_protocols (
  run_id TEXT NOT NULL,
  protocol_id TEXT NOT NULL,
  PRIMARY KEY (run_id, protocol_id)
);

CREATE INDEX IF NOT EXISTS idx_public_report_run_protocols_protocol_id ON public_report_run_protocols(protocol_id);

CREATE TABLE IF NOT EXISTS public_report_run_warnings (
  run_id TEXT NOT NULL,
  warning_index INTEGER NOT NULL,
  warning_source TEXT NOT NULL,
  warning_code TEXT NOT NULL,
  warning_message TEXT NOT NULL,
  PRIMARY KEY (run_id, warning_index)
);

CREATE INDEX IF NOT EXISTS idx_public_report_run_warnings_source ON public_report_run_warnings(warning_source);
CREATE INDEX IF NOT EXISTS idx_public_report_run_warnings_code ON public_report_run_warnings(warning_code);

CREATE TABLE IF NOT EXISTS public_report_run_object_keys (
  run_id TEXT NOT NULL,
  object_kind TEXT NOT NULL,
  object_key TEXT NOT NULL,
  PRIMARY KEY (run_id, object_kind)
);

CREATE INDEX IF NOT EXISTS idx_public_report_run_object_keys_object_kind ON public_report_run_object_keys(object_kind);

CREATE TABLE IF NOT EXISTS public_report_latest (
  singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
  run_id TEXT NOT NULL,
  generated_at TEXT NOT NULL,
  published_at TEXT NOT NULL,
  claim_level TEXT NOT NULL,
  publishable INTEGER NOT NULL,
  diagnostic_only INTEGER NOT NULL,
  execution_profile TEXT NOT NULL,
  visibility TEXT NOT NULL,
  source_kind TEXT NOT NULL,
  evidence_warning_count INTEGER NOT NULL,
  publication_warning_count INTEGER NOT NULL,
  copied_artifact_count INTEGER NOT NULL,
  skipped_artifact_count INTEGER NOT NULL,
  implementation_count INTEGER NOT NULL,
  scenario_count INTEGER NOT NULL,
  protocol_count INTEGER NOT NULL,
  validation_passed INTEGER NOT NULL,
  validation_failed INTEGER NOT NULL,
  validation_unsupported INTEGER NOT NULL,
  validation_not_applicable INTEGER NOT NULL,
  validation_inconclusive INTEGER NOT NULL,
  validation_infrastructure_failure INTEGER NOT NULL,
  benchmark_accepted INTEGER NOT NULL,
  benchmark_rejected INTEGER NOT NULL,
  benchmark_not_run_validation_failed INTEGER NOT NULL,
  benchmark_not_run_unsupported INTEGER NOT NULL,
  benchmark_not_run_load_tool_failed INTEGER NOT NULL,
  benchmark_not_run_parser_failed INTEGER NOT NULL,
  artifact_root_key TEXT NOT NULL,
  bundle_prefix TEXT NOT NULL,
  evidence_report_json_key TEXT NOT NULL,
  evidence_report_markdown_key TEXT NOT NULL,
  artifacts_index_key TEXT NOT NULL,
  publication_manifest_key TEXT NOT NULL,
  publication_warnings_key TEXT NOT NULL,
  publication_skipped_key TEXT NOT NULL,
  report_index_entry_key TEXT NOT NULL,
  report_index_key TEXT NOT NULL,
  registry_key TEXT NOT NULL,
  latest_key TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_public_report_latest_generated_at ON public_report_latest(generated_at DESC);
