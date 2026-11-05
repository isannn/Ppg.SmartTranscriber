# FR-052 — Webhook delivery log

Status: Approved (autonomous)
Jira: ST-53

## What
Webhook attempts (global endpoints + per-job callback_url) are
fire-and-forget: failures only hit logs. Persist every attempt so
operators/bots can audit flaky callbacks.

## Changes
- Migration `0014_webhook_deliveries.sql`: job_id (FK cascade),
  url, event, status_code, success, error, duration_ms, created_at.
- `WebhookDelivery` entity + `IWebhookDeliveryRepository` port +
  Dapper repo; `WebhookDeliveryDto`.
- `HttpWebhookNotifier` records each attempt (status code, success
  = 2xx, exception text, elapsed ms) — still best-effort.
- `GET /api/v1/webhook-deliveries?jobId=&limit=` + CLI
  `api deliveries [--job ID] [--limit N]`.

## Acceptance
- After a job finishes, one row per endpoint attempted with the
  HTTP status; failed deliveries show the error.
- Endpoint + CLI return recent deliveries.
