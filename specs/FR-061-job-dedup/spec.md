# FR-061 — Job deduplication (URL + content hash)

Status: Draft

Jira: ST-62

## Why

`POST /api/v1/jobs/from-url` currently creates a brand-new job for every
submission. The same URL submitted twice runs the full
download → upload → transcribe → summarize pipeline twice; two different
URLs pointing at the same media do the same. There is no URL
normalization, no URL lookup, and no content hashing anywhere in the
ingest path. Transcription is the most expensive stage (GPU time), so
duplicates are pure waste.

Two dedup levels:

1. **URL level** — a normalized/canonical URL identity checked before a
   job row is created.
2. **Content level** — a SHA-256 hash of the downloaded/uploaded media,
   checked once the bytes are available, reusing a prior job's results
   instead of retranscribing.

## Requirements

| Req | Description |
|---|---|
| FR-061.1 | `UrlNormalizer` in Core: lowercase scheme/host, strip default port, strip fragment, drop tracking params (`utm_*`, `fbclid`, `gclid`, `si`, `feature`), YouTube canonicalization (`youtu.be/`, `watch?v=`, `/shorts/`, `/embed/`, `/live/` → `youtube.com/watch?v=<id>`), remaining query params sorted. |
| FR-061.2 | Migration `0017_job_dedup.sql`: `transcription_jobs` gains `source_url text NULL` and `content_hash text NULL`; unique partial index `ux_jobs_source_url` on `source_url` where `source_url IS NOT NULL AND status <> 'Failed'`; index `ix_jobs_content_hash` on `content_hash`. No backfill of `content_hash` (needs file bytes); `source_url` backfilled best-effort for URL jobs still in `Downloading`. |
| FR-061.3 | `CreateUrlJobAsync` normalizes the URL, stores it in `source_url`. If a live (non-`Failed`) job already owns that `source_url`: (a) canonical `Done` → the new request clones its results into a new job (see FR-061.5) — every submission gets its own job row; (b) canonical in-flight (`Downloading`/`Pending`/`Processing`) → the API returns the existing job with `duplicate: true`, no new row. Canonical `Failed` → treated as no match, a fresh job is created and re-claims the URL. |
| FR-061.4 | The unique index is the race guard for URL dedup: on a 23505 unique violation the service re-reads the existing row and applies the same (a)/(b) rule instead of erroring. |
| FR-061.5 | **Result clone**: a new job row that inherits `ObjectName` (shared S3 object), `DurationSeconds`, `MediaMeta`, `Model`, `Language`, `Summary`, `Translation`/`TranslationLang` and a copy of all `TranscriptSegments` (new rows, new `job_id`); `Status = Done`, `Progress = 100`, `CompletedAt = now`, `content_hash` copied, `source_url` = the submitted normalized URL (or NULL when it is already claimed). Request params (`CallbackUrl`, `Hotwords`, `Priority`, `Tags`) stay on the new job; its own webhook fires on completion. |
| FR-061.6 | **Content hash**: SHA-256 (lowercase hex) is computed for every ingested file — URL jobs after `IMediaFetcher.FetchAsync` (before S3 upload) and direct uploads while the request stream is spooled to the temp file. If a `Done` job with the same `content_hash` exists and its `Hotwords`/`Diarize` match the request, the new job clones results per FR-061.5 (URL jobs skip the S3 upload entirely; upload jobs skip the already-spooled upload). Otherwise the job proceeds normally with `content_hash` recorded. Content matches on in-flight or `Failed` jobs do not dedup — the job proceeds normally. |
| FR-061.7 | `DeleteJobAsync(deleteFile: true)` skips the S3 delete when another job row still references the same `ObjectName` (shared artifact safety). |
| FR-061.8 | API contract: `POST /from-url` returns `202 Accepted {id, status}` for a genuinely new job as today; `200 OK {id, status, duplicate: true, duplicateOf}` when it resolves to an existing/derived result. Same `duplicate`/`duplicateOf` fields on `JobDto` so UI/CLI can surface "duplicate of <id>". |
| FR-061.9 | Retry of a `Failed` job is unchanged; if its `source_url` has since been claimed by a live job, the retried job clears its own `source_url` (keeps its artifact) to satisfy the unique index. |

## Non-requirements

- No dedup for in-flight content matches — only `Done` canonicals are
  cloned. Two simultaneous submissions of the same content may both
  transcribe (documented limitation).
- No fuzzy/near-duplicate detection — exact SHA-256 of the stored bytes
  only. Different encodings/qualities of the same video are distinct.
- No voiceprint/speaker-embeddings cloning in v1; copied segments keep
  their speaker labels.
- No UI work beyond exposing the `duplicate`/`duplicateOf` fields
  (a badge is optional polish, not required).
- `GET` endpoints, list views, search behavior unchanged.
- No changes to `IMediaFetcher` (yt-dlp) — hashing happens after fetch.

## Acceptance

- `dotnet build` + NUnit green.
- `UrlNormalizer` unit tests: YouTube variants (`watch`, `youtu.be`,
  `shorts`, tracking params) normalize to one key; non-YouTube URLs keep
  meaningful query params, sorted.
- Same URL twice → second call returns `duplicate: true` (or a clone with
  copied results when canonical is `Done`); DB holds one live owner of
  `source_url`.
- Two different URLs / an upload + a URL yielding identical bytes → the
  second job is `Done` with cloned segments and shared `ObjectName`, no
  second S3 object, no STT call (assertable via fakes).
- Concurrent identical submissions → exactly one pipeline run, both
  callers get a valid response (23505 path covered).
- `Failed` canonical → resubmission creates a fresh job.
- Shared-object delete: deleting the clone keeps the file while the
  canonical job still references it.
- Migration applies cleanly on the dev `db_smarttranscriber` via DbUp.

## Open decisions (to confirm at spec approval)

- In-flight URL hit returns the existing job (`duplicate: true`) rather
  than registering the new request's own callback/tags — per-request
  webhooks do not fire for that case.
- Content-dedup requires matching `Hotwords`+`Diarize`; `Priority`,
  `Tags`, `CallbackUrl` do not affect transcript identity.
