# FR-063 plan: retry of failed URL-ingest jobs

- Status: Approved (spec)

## Approach

Detect ingest-failed jobs by the `url/` placeholder still sitting in
`ObjectName` and send them back through `IngestUrlAsync` instead of the
transcription queue. No schema changes; detection relies on the existing
convention (`CreateUrlJobAsync` writes `url/{guid}`, successful ingest
always overwrites it with `{jobId}_{file}`).

### RetryOutcome enum

`IJobService.RetryJobAsync` currently returns `Task<bool?>`
(null/true/false) — too narrow to express "reingesting". Replace with
`Task<RetryOutcome>` (`NotFound | NotFailed | Queued | Reingesting`),
a new enum in `Core/Domain` next to `JobStatus`. Single call site changes:
`JobsController.Retry` and `JobService.BulkAsync`.

### JobService changes (`src/SmartTranscriber.Core/Services/JobService.cs`)

- `RetryJobAsync`:
  - `job is null` → `NotFound`; `Status != Failed` → `NotFailed`.
  - `job.ObjectName.StartsWith("url/", Ordinal)` → reset to
    `Downloading`, `Progress = 2`, clear `ErrorMessage`/`CompletedAt`,
    `UpdateAsync`, then `_ = Task.Run(() => IngestUrlAsync(job.Id,
    job.OriginalFileName))`; return `Reingesting`.
    (`OriginalFileName` still holds the URL — it is only replaced by the
    media file name on the ingest success path.)
  - otherwise existing path: `Pending` + enqueue → `Queued`.
- `IngestUrlAsync(Guid, string)` — make private signature tolerant: no
  changes needed, it is already fire-and-forget.
- `BulkAsync` `retry` detail strings: `"reingesting"` / `"queued"` /
  `"not Failed"` / `"not found"` — maps `RetryOutcome` 1:1.

### Controller (`JobsController.Retry`)

- `NotFound` → 404; `NotFailed` → 409 same body;
  `Queued` → `202 { id, status: "Pending" }` (unchanged);
  `Reingesting` → `202 { id, status: "Downloading" }`.

### Pipeline defense (`TranscriptionPipeline.ProcessAsync`)

Right after the `GetByIdAsync` null-check, before any work:

```csharp
if (job.ObjectName.StartsWith("url/", StringComparison.Ordinal))
{
    // ingest placeholder — media was never uploaded; surface a clear
    // failure instead of a raw storage ObjectNotFoundException
    job.Status = JobStatus.Failed;
    job.ErrorMessage = "job has no media object — retry to re-download";
    job.CompletedAt = DateTime.UtcNow;
    await jobs.UpdateAsync(job, CancellationToken.None);
    progressSink.Report(job.Id, job.Progress, JobStatus.Failed);
    return;
}
```

This covers every path that could land an `url/` job in the queue
(stale DB rows, manual UPDATE, race with FR-061 clones).

## Affected files

- `src/SmartTranscriber.Core/Domain/RetryOutcome.cs` (new enum)
- `src/SmartTranscriber.Core/Services/IJobService.cs` (signature)
- `src/SmartTranscriber.Core/Services/JobService.cs`
  (`RetryJobAsync`, `BulkAsync` detail)
- `src/SmartTranscriber.Api/Controllers/JobsController.cs` (`Retry` map)
- `src/SmartTranscriber.Infrastructure/Transcription/TranscriptionPipeline.cs`
  (url/ guard)
- `src/SmartTranscriber.Cli/Program.cs` — no change needed: `api retry`
  prints the API response body already
- `tests/SmartTranscriber.Tests/JobRetryTests.cs` (new)
- `README.md` — retry semantics paragraph

## Tests

`JobService` deps are all interfaces — hand-rolled in-memory fakes (no
mock lib in the test project; `JobDedupTests` on FR-061 branch follows
the same pattern):

- `Retry_ingest_failed_job_goes_to_Downloading_and_resets_error`
- `Retry_transcribe_failed_job_goes_to_Pending` (regression)
- `Retry_done_or_pending_returns_NotFailed`
- `Pipeline_refuses_url_placeholder_object` — pipeline-level guard can be
  covered indirectly via the `ObjectName` prefix predicate if extracted
  to `TranscriptionJob`/static helper; otherwise a lightweight direct
  test of the guard predicate.

## Dependencies / order

One task branch (`tasks/task-N-url-ingest-retry` off the feature branch)
or direct work on `features/FR-063-url-ingest-retry` — feature is small
enough for a single task. Build + NUnit green before MR to dev.
