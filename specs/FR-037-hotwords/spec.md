# FR-037 — Hotwords/glossary per job

Status: Approved (autonomous)
Jira: ST-38

Per-job glossary bias: `hotwords` (comma-separated terms) on create —
stored on the job (migration 0009), pipeline passes into
TranscribeOptions.Hotwords → sidecar query param →
core.transcribe_words(hotwords=...). Inputs: ?hotwords= on /files +
/files/batch, {hotwords} on /from-url and /retranscribe, CLI --hotwords.
