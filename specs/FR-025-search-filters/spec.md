# FR-025 — Search filters

Status: Draft
Jira: ST-26

GET /api/v1/search?q= gains optional filters: jobId, speaker, from, to,
model — conditional WHERE clauses, ts_headline unchanged.
CLI: api search <q> [--job ID] [--speaker S] [--from D] [--to D] [--model M]
