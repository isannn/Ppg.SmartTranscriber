# FR-064: Job cancellation endpoint

- Status: Draft
- Jira: ST-65
- Branch: `features/FR-064-job-cancel`

## Problem

There is no way to stop a job once submitted. A stuck `Downloading` job
(hung yt-dlp, slow fetch) or an accidental duplicate `from-url` can only
be waited out; after a process restart the sweep may even resurrect it.
Observed: a duplicate URL job continued transcription alongside the
recovered original, doubling load for ~40 min.

## Scope

- `POST /api/v1/jobs/{id}/cancel` transitions a job to a new terminal
  `Cancelled` status and aborts in-flight work where technically
  possible.
- Covered statuses: `Pending` (unclaim — never starts), `Downloading`
  (abort the `IngestUrlAsync` task), `Processing` (cooperative cancel —
  stops at the next pipeline checkpoint / kills the STT call via
  cancellation token).
- Terminal statuses (`Done`, `Failed`, `Cancelled`) → `400` with a
  message; unknown id → `404`.
- CLI: `smarttranscriber api cancel <id>`. Blazor UI: Cancel button on
  non-terminal job rows.
- Out of scope: bulk cancel (the bulk endpoint can be extended later),
  deleting media of a cancelled job (manual delete covers it).

## Requirements

### Functional

- **FR-064.1** `JobStatus.Cancelled` added (terminal; `CompletedAt` set).
  Migration if the enum is persisted as text — none needed (string
  column, enum parse on read already handles it).
- **FR-064.2** `IJobService.CancelJobAsync(id)`:
  - `Downloading` → set `Cancelled` and signal the per-ingest
    `CancellationTokenSource`; `IngestUrlAsync` observes it through
    `fetcher.FetchAsync(url, dir, ct)` (yt-dlp process killed) and
    S3 upload calls.
  - `Pending` → `Cancelled`; the job is skipped because
    `ClaimNextPendingAsync` only claims `Pending` — and the pipeline
    re-checks status after claim (defense against a claim/cancel race).
  - `Processing` → `Cancelled` in DB plus a cooperative token the
    `TranscriptionPipeline` consults between stages (download → probe →
    STT → persist); STT HTTP calls get the same token.
- **FR-064.3** In-flight cancellation registry:
  `ConcurrentDictionary<Guid, CancellationTokenSource>` (singleton),
  populated by `IngestUrlAsync` and the pipeline, removed on
  completion/cancel. `CancelJobAsync` cancels the CTS if present — no
  cross-process coordination needed (single worker host).
- **FR-064.4** `IngestUrlAsync` and `TranscriptionPipeline` pass a linked
  token (job CTS + host token) instead of running untracked / on the host
  stop token. On `OperationCanceledException` where the job CTS fired
  (not host shutdown): status `Cancelled`, `CompletedAt` set, webhook
  `NotifyJobFinishedAsync` fired — same as other terminal states.
- **FR-064.5** A `Cancelled` job can be retried: `RetryJobAsync` accepts
  `Cancelled` in addition to `Failed` (re-ingest path for `url/` objects
  per FR-063, else straight to `Pending`).
- **FR-064.6** API: `POST /api/v1/jobs/{id}/cancel` → `200` with the
  updated DTO (`status: Cancelled`); `400` when already terminal;
  `404` unknown id. CLI `api cancel` prints the resulting status.
- **FR-064.7** `ResetInterruptedJobsAsync` treats `Cancelled` as terminal
  (it already only touches `Processing`/`Downloading`; verify a job
  cancelled mid-ingest is not resurrected on restart).
- **FR-064.8** UI: Jobs page shows Cancel on `Pending`/`Downloading`/
  `Processing` rows; `Cancelled` renders as a terminal status pill.
  SignalR progress sink reports `Cancelled` like other terminal states.

### Non-functional

- No DB schema change (status is a text column); no migration.
- Cancellation is best-effort for `Processing` — a job may complete
  between the cancel request and the next checkpoint; that is acceptable
  and must not error the endpoint (cancel wins only if the status is
  still `Cancelled` at persist time — guard the final `UpdateAsync`
  writes so a done-write does not resurrect a cancelled job... or accept
  last-write-wins; choose in plan).
- All new log/console strings in English per constitution.

## Acceptance criteria

1. `POST /jobs/{id}/cancel` on a `Pending` job → `200`, job shows
   `Cancelled`, sweep never claims it.
2. On a `Downloading` job → ingest aborts (yt-dlp process exits), job
   ends `Cancelled` with `CompletedAt` set; restart does not resurrect it.
3. On a `Processing` job → pipeline stops at the next checkpoint and
   persists `Cancelled` (no `Failed` written for a user-initiated cancel).
4. `Done`/`Failed`/`Cancelled` → `400`; unknown id → `404`.
5. Retrying a `Cancelled` job works (FR-063 semantics for `url/` jobs).
6. `dotnet build` + NUnit green; new tests cover the status matrix.
