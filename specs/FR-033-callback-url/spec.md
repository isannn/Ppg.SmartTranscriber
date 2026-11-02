# FR-033 — Per-job callback_url

Status: Draft
Jira: ST-34

Optional `callback_url` on the job (migration 0006) — supplied via
create-request (`POST /files`, `POST /jobs/from-url`, CLI --callback).
HttpWebhookNotifier posts to it on Done/Failed in addition to global
Webhooks:Endpoints. JobDto exposes callback_url.
