# FR-061 — Plan: job deduplication

Status: Draft

Spec: `specs/FR-061-job-dedup/spec.md` (Approved). Jira: ST-62.

## Approach

Two dedup points, one shared "clone results" helper.

1. **URL level (pre-insert).** `CreateUrlJobAsync` normalizes the URL and
   calls a new atomic repo op `GetOrAddUrlJobAsync` — `INSERT … ON
   CONFLICT (source_url) … DO NOTHING` guarded by the partial unique
   index `ux_jobs_source_url (source_url) WHERE source_url IS NOT NULL
   AND status <> 'Failed'`; on conflict it re-reads and returns the
   owner. Owner `Done` → clone into a fresh job row; owner in-flight →
   return it with `DuplicateOf` set (no new row); owner `Failed` is
   invisible to the index → insert proceeds normally.
2. **Content level (post-bytes).** SHA-256 hex computed:
   - URL jobs — in `IngestUrlAsync` right after `FetchAsync`, before S3
     upload; a `Done` hash-owner → the already-created `Downloading` row
     resolves to a clone (shared `ObjectName`, copied
     transcript/summary/meta) and skips upload + enqueue.
   - Uploads — `CreateJobAsync` spools the request stream to a temp file
     while hashing (replaces the direct pass-through stream); hash hit →
     clone row without upload; miss → upload the spooled file, then
     create + enqueue as today. Temp file always removed.
3. **Clone helper** (`CloneFromAsync`): copies `ObjectName`,
   `ContentHash`, `DurationSeconds`, `MediaMeta`, `Model`, `Language`,
   `Summary`, `Translation`, `TranslationLang`; bulk-copies segments via
   `ISegmentRepository` (new `job_id`); sets `Done`/`Progress=100`/
   `CompletedAt`; `DuplicateOf = canonical.Id`; fires
   `NotifyJobFinishedAsync` so the new job's own callback/webhook runs.
   `SegmentCount` follows the copied rows.

## Files

| File | Change |
|---|---|
| `db/Migrations/0017_job_dedup.sql` | `+source_url`, `+content_hash`, `+duplicate_of`; `ux_jobs_source_url` (partial unique, excludes `Failed`), `ix_jobs_content_hash`; best-effort `source_url` backfill for `Downloading` rows whose `original_file_name` looks like a URL |
| `Core/Domain/TranscriptionJob.cs` | `SourceUrl`, `ContentHash`, `DuplicateOf` |
| `Core/Dto/Dtos.cs` | `JobDto` += `SourceUrl?`, `DuplicateOf?` (trailing optional params; `Duplicate` computed in JSON layer — see controller) |
| `Core/Services/UrlNormalizer.cs` | **new** — pure normalizer per FR-061.1 |
| `Core/Ports/IJobRepository.cs` | `GetOrAddUrlJobAsync`, `FindDoneByContentHashAsync`, `CountByObjectNameAsync` |
| `Core/Services/JobService.cs` | dedup in `CreateUrlJobAsync`/`CreateJobAsync`/`IngestUrlAsync`; `CloneFromAsync` helper; `DeleteJobAsync` shared-object guard; `Map` new fields |
| `Core/Services/IJobService.cs` | signature stays `Task<JobDto>` — dedup outcome carried on the DTO (`DuplicateOf`) |
| `Infrastructure/Persistence/JobRepository.cs` | columns in `JobColumns`/insert/update; the three new queries (`INSERT … ON CONFLICT DO NOTHING` + re-select; `status='Done'` hash lookup newest-first; `COUNT(*)`) |
| `Api/Controllers/JobsController.cs` | `from-url`: `DuplicateOf is not null` → `200 OK {id,status,duplicate,duplicateOf}`, else `202` as today |
| `Api/Controllers/FilesController.cs` | unchanged — dedup rides inside `JobDto` |
| `tests/…/UrlNormalizerTests.cs` | **new** — YouTube/tracking-param/param-order cases |
| `tests/…/JobDedupTests.cs` | **new** — service-level tests with in-memory fake repos/ports: same-URL in-flight → no new row; same-URL Done → clone; hash hit on upload path → clone, no storage upload; hash miss → normal path; Failed owner → fresh job |

## Decisions

- `GetOrAddUrlJobAsync` in the repo (not check-then-insert in the
  service) — the unique index only protects against races if insert and
  conflict handling are one roundtrip; `ON CONFLICT … DO NOTHING` +
  re-select is race-free without catching `PostgresException` in Core
  (which must stay infra-free).
- Upload hashing via temp spool, not a tee-stream into S3: saves the
  redundant upload on a hit and mirrors the URL path (file on disk →
  hash → upload-or-reuse).
- Clone shares `ObjectName` (no S3 copy) + `CountByObjectNameAsync`
  guard on delete — zero extra storage for identical media.
- `DuplicateOf` persisted on clones so `GET /jobs/{id}` later still
  shows provenance; the `duplicate` response flag is derived
  (`DuplicateOf != null`), not stored.
- Voiceprint rows are not cloned (spec non-requirement); segment
  speaker labels carry over in the copied rows.

## Risks

- `ON CONFLICT` partial-index inference: predicate in the insert must
  textually match the index predicate — covered by an integration
  smoke (`dotnet build` + manual `psql` check post-migration).
- yt-dlp output may differ per-download (metadata/muxing) → same video
  can hash differently; exact-bytes dedup is a documented limit.
- Stale Failed rows keep `source_url` values — harmless (index ignores
  them) and preserves history.

## Verification

- `dotnet build` + `dotnet test` (NUnit) green.
- Apply migration against dev `db_smarttranscriber`; verify indexes via
  `\d transcription_jobs`.
- Manual smoke on :8460: same YouTube URL twice → second response
  `duplicate: true`; different URL same video → clone `Done` without
  STT; delete clone → object survives while canonical lives.
