# FR-020 — Webhook/notify on job completion

Status: Draft
Jira: ST-21

## What / Why

Bots (devin_main_bot, TeleCoder) currently must poll `GET /jobs/{id}`.
A webhook on Done/Failed closes the loop — push instead of poll.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-020.1 | `IWebhookNotifier` port + `HttpWebhookNotifier` — POST JSON to every endpoint in `Webhooks:Endpoints` (env `SMARTTRANSCRIBER__Webhooks__Endpoints__0`, `__1`…) |
| FR-020.2 | Payload: `{event:"job.finished", jobId, status, originalFileName, errorMessage, model, durationSeconds, completedAt}` |
| FR-020.3 | Fired on Done and Failed — pipeline end states AND url-ingest failures (JobService) |
| FR-020.4 | Best-effort: 10s timeout, errors logged, never thrown; no endpoints = silent no-op |

## Acceptance criteria

1. With `Endpoints[0]=http://127.0.0.1:8499/hook`, a finishing job produces a POST with status + jobId at the listener
