# FR-063 tasks

- **task-1** — implement + tests (single unit of work, feature branch
  `features/FR-063-url-ingest-retry`):
  1. `RetryOutcome` enum (Core/Domain)
  2. `RetryJobAsync` → enum result + `url/` re-ingest branch
  3. `BulkAsync` detail strings
  4. `JobsController.Retry` → `Downloading`/`Pending` per outcome
  5. `TranscriptionPipeline` `url/` guard → clear Failed
  6. `JobRetryTests` (both branches + status guards)
  7. README retry paragraph
  8. `dotnet build` + `dotnet test` green
