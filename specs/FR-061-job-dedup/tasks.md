# FR-061 — tasks

Single feature branch `features/FR-061-job-dedup` (no task branches).

- task-1: migration `0017_job_dedup.sql` + `TranscriptionJob`/`JobDto`/
  `JobRow` fields (`source_url`, `content_hash`, `duplicate_of`) +
  repo column plumbing.
- task-2: `UrlNormalizer` (Core, pure) + `UrlNormalizerTests`.
- task-3: repo ops — `GetOrAddUrlJobAsync` (ON CONFLICT + re-select),
  `FindDoneByContentHashAsync`, `CountByObjectNameAsync`.
- task-4: service — `CreateUrlJobAsync` URL dedup; `CloneFromAsync`
  helper; `IngestUrlAsync` hash + clone-resolve; `CreateJobAsync`
  spool+hash+clone-or-upload; `DeleteJobAsync` shared-object guard;
  controller 200/202 + `duplicate` response fields.
- task-5: `JobDedupTests` with in-memory fakes — in-flight URL hit,
  Done URL clone, upload hash hit/miss, Failed owner re-submit,
  shared-object delete guard.
- task-6: `dotnet build` + `dotnet test`; DbUp migration smoke on dev
  `db_smarttranscriber`; manual API smoke (same URL twice, same bytes
  twice); features.md → MR state.
