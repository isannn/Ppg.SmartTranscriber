# FR-063: Retry of failed URL-ingest jobs re-ingests media

- Status: Draft
- Jira: ST-64
- Branch: `features/FR-063-url-ingest-retry`

## Problem

A `from-url` job that fails during media ingest (yt-dlp error, network,
storage unreachable) never gets a real S3 object — `job.ObjectName` keeps
the `url/{guid}` placeholder. `RetryJobAsync` treats every `Failed` job the
same: it flips the status to `Pending` and enqueues it for transcription.
The pipeline then calls `storage.DownloadAsync("url/…")`, gets
`ObjectNotFoundException`, and overwrites `job.ErrorMessage` with it —
masking the original ingest error (e.g. the YouTube bot-check
"Sign in to confirm you're not a bot"). Observed on job `07bbe198-…`.

The retry is also semantically useless: there is nothing to transcribe,
the job is guaranteed to re-fail — it just wastes a queue slot and
destroys diagnostics.

## Scope

- `POST /jobs/{id}/retry` (and the `retry` bulk action) for jobs that
  failed **during URL ingest** re-runs `IngestUrlAsync` instead of
  enqueueing for transcription.
- Detection of "failed during ingest" is schema-free: such jobs still
  carry `ObjectName` with the `url/` prefix (the placeholder assigned in
  `CreateUrlJobAsync`) — a completed ingest always replaces it with the
  real `{jobId}_{file}` object name.
- Out of scope: dedup of `from-url` submissions (FR-061, MR !59), job
  cancellation endpoint (FR-064), ingest progress reporting.

## Requirements

### Functional

- **FR-063.1** `RetryJobAsync` detects ingest-failed jobs via the `url/`
  `ObjectName` prefix. For them it resets status to `Downloading`,
  `Progress = 2`, clears `ErrorMessage`/`CompletedAt`, persists the update,
  and launches `IngestUrlAsync` in the background — same as
  `CreateUrlJobAsync` does for a fresh job.
- **FR-063.2** The URL for re-ingest is taken from `job.OriginalFileName`
  (for ingest-failed jobs it still holds the submitted URL — the field is
  only replaced with the media file name on the ingest success path).
- **FR-063.3** Jobs that failed at any later stage (Pending/Processing
  failures) keep the existing retry semantics — straight to `Pending` +
  enqueue.
- **FR-063.4** The API response distinguishes the two paths so callers can
  tell "re-downloading" from "re-transcribing" — e.g. a `reingested` flag
  in the bulk result detail and a log line; the single-job endpoint keeps
  its current contract (`200`/`404`/`400`), returning the updated job DTO
  (`status: Downloading`).
- **FR-063.5** `IngestUrlAsync` no longer overwrites a meaningful error
  with a secondary one: on the happy path nothing changes; on failure it
  still writes `url ingest: <ex.Message>` — and the pipeline must never
  see a job whose `ObjectName` still points at the `url/` placeholder.
  Defense in depth: `TranscriptionPipeline.ProcessAsync` fails fast with
  a clear message ("job has no media object — retry to re-download") if a
  claimed job still carries an `url/` object name, instead of surfacing a
  raw storage exception as the job error.
- **FR-063.6** Concurrent duplicate `from-url` submissions for the same
  URL are out of scope (FR-061 unique index); a manual retry launched
  while a previous `IngestUrlAsync` for the same job is still running is
  tolerated — ingest is idempotent per job id (same `ObjectName` target,
  last `UpdateAsync` wins).

### Non-functional

- No schema changes (no migration).
- Npgsql resilience (separate non-code item approved with this spec):
  the production `SMARTTRANSCRIBER__ConnectionString` should carry
  `Command Timeout=60;Keepalive=30` to smooth read timeouts observed
  under transcription load; documented in the spec so the value lands in
  Infisical (`/smarttranscriber`) — secrets stay out of the repo.
- Docs (README/API section) updated to describe the two retry paths.

## Acceptance criteria

1. A job failed during URL ingest, retried via `POST /jobs/{id}/retry`,
   transitions `Failed → Downloading` and its media is re-fetched: on
   success it proceeds `Pending → Processing → Done`; the original
   `ObjectName` placeholder is replaced by the real object key.
2. The same job, when the download fails again, shows
   `errorMessage = "url ingest: <root cause>"` — never a MinIO
   `ObjectNotFoundException` about the `url/` placeholder.
3. A job that failed during transcription still retries straight into
   transcription (existing behavior, regression check).
4. Bulk `retry` action applies the same logic per job and reports the
   ingest path in its detail string.
5. `dotnet build` clean; new unit tests for `RetryJobAsync` cover both
   branches (url-prefixed vs normal object name).
