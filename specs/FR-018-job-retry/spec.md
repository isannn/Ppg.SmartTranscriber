# FR-018 — Retry failed jobs

Status: Draft
Jira: ST-18

## What / Why

A Failed job (sidecar down, transient S3 error) needs a one-action retry
instead of re-uploading the file.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-018.1 | `POST /api/v1/jobs/{id}/retry` — only for `Failed` jobs: status→Pending, error cleared, progress→5, re-enqueued; 409 otherwise |
| FR-018.2 | `IJobService.RetryJobAsync` → `bool?` (null=not found, false=not Failed) |
| FR-018.3 | Jobs UI: retry button on Failed rows; CLI `api retry <id>` |
| FR-018.4 | Existing media object is reused — no re-upload |

## Acceptance criteria

1. Failed job → POST retry → Pending → processed again
2. Retry on non-Failed job → 409
