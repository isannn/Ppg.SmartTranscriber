# FR-064 plan: job cancellation endpoint

- Status: Approved (spec)
- Base: `features/FR-063-url-ingest-retry` (stacked — uses `RetryOutcome`
  and the url/ re-ingest retry path; rebases cleanly once FR-063 merges)

## Approach

New terminal status `Cancelled`; a per-job `CancellationTokenSource`
registry lets `POST /jobs/{id}/cancel` abort in-flight work
cooperatively: mark the job `Cancelled` in the DB first, then cancel the
registered token so ingest/pipeline stop at the next await.

## Components

### 1. `JobStatus.Cancelled` (`Core/Domain/JobStatus.cs`)

Plain enum member; persisted as the existing text column — no migration.
`Enum.Parse` on read handles it. Every place that treats `Done|Failed`
as terminal must learn the new value (see affected files).

### 2. `JobCancellationRegistry` (new, `Core/Services/`)

Concrete singleton (no port — an in-process coordination primitive):

```csharp
public sealed class JobCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cts = new();
    public CancellationTokenSource Register(Guid jobId);
    public bool TryCancel(Guid jobId);   // false when nothing is in flight
    public void Remove(Guid jobId);      // dispose + drop
}
```

Registered via `services.AddSingleton<JobCancellationRegistry>()` in
`DependencyInjection`. Scoped consumers (`JobService`,
`TranscriptionPipeline`, `IngestUrlAsync` scope) all see the same
instance.

### 3. `JobService` (`Core/Services/JobService.cs`)

- New ctor dep `JobCancellationRegistry cancellation`.
- `CancelJobAsync(id)` → `Task<bool?>`:
  `null` = unknown id; `false` = already terminal (`Done|Failed|Cancelled`);
  else set `Cancelled` + `CompletedAt`, `UpdateAsync`,
  `cancellation.TryCancel(id)`, `webhooks.NotifyJobFinishedAsync`, `true`.
- `IngestUrlAsync` gets `var cts = cancellation.Register(jobId)` at the
  top; all awaits use `cts.Token` (fetch/upload/exists/repo).
  `catch (OperationCanceledException)` → job ends `Cancelled` (no
  `url ingest:` error written); `finally` → `cancellation.Remove(jobId)`.
- `RetryJobAsync` accepts `Failed or Cancelled` (FR-064.5); url/ branch
  unchanged (FR-063).

### 4. `TranscriptionPipeline` (`Infrastructure/Transcription/`)

- `jobCts = cancellation.Register(jobId)` at the top of `ProcessAsync`;
  `finally` removes it.
- Early exit on `job.Status == JobStatus.Cancelled` right after the
  initial `GetByIdAsync` — closes the "cancelled between claim and cts
  registration" race. (A cancel landing before `Register` simply finds
  no cts; the row is already `Cancelled` and we bail here.)
- `using var linked = CancellationTokenSource.CreateLinkedTokenSource(
  ct, jobCts.Token)`; every await inside the working path takes
  `linked.Token` (storage download, probe, transcriber, repo updates,
  post-steps).
- Catch order — job-cancel checked BEFORE host-shutdown:
  `OCE when (jobCts.IsCancellationRequested)` → status `Cancelled`,
  `CompletedAt`, `UpdateAsync`, `progressSink`, webhook; swallow (normal
  termination, do not rethrow).
  `OCE when (ct.IsCancellationRequested)` → existing `Pending` requeue.

### 5. `YtDlpMediaFetcher` — actually kill yt-dlp on cancel

Today the linked-CTS timeout fires `WaitForExitAsync` but the child
process is never killed. Add
`cts.Token.Register(() => proc.Kill(entireProcessTree: true))`
(best-effort try/catch) so both cancel and timeout reap the process.

### 6. API + CLI + UI

- `JobsController`: `POST {id}/cancel` → `200 {id,status:"Cancelled"}` /
  `400 {error}` terminal / `404`. OpenAPI doc is generated from
  controllers — no manual spec work.
- CLI `api cancel <id>` mirrors `api retry`.
- `Jobs.razor`: Cancel button on `Pending|Downloading|Processing` rows;
  Retry button extended to `Cancelled`; terminal-set expressions
  (`is not ("Done" or "Failed")` + `nameof(...)` progress bar) gain
  `Cancelled`; same terminal-set fix in `Transcript.razor` if it filters
  JoinJob by status.

### 7. `ResetInterruptedJobsAsync`

Unchanged — already touches only `Processing`/`Downloading`; a job
`Cancelled` mid-ingest stays `Cancelled` across restarts (spec FR-064.7).

## Affected files

- `src/SmartTranscriber.Core/Domain/JobStatus.cs`
- `src/SmartTranscriber.Core/Services/JobCancellationRegistry.cs` (new)
- `src/SmartTranscriber.Core/Services/IJobService.cs`, `JobService.cs`
- `src/SmartTranscriber.Infrastructure/DependencyInjection.cs`
- `src/SmartTranscriber.Infrastructure/Transcription/TranscriptionPipeline.cs`
- `src/SmartTranscriber.Infrastructure/Media/YtDlpMediaFetcher.cs`
- `src/SmartTranscriber.Api/Controllers/JobsController.cs`
- `src/SmartTranscriber.Cli/Program.cs`
- `src/SmartTranscriber.Api/Components/Pages/Jobs.razor` (+ `Transcript.razor` if filtered)
- `tests/SmartTranscriber.Tests/JobCancelTests.cs` (new)
- `readme.md` (endpoint table row)

## Tests

- `Cancel_Pending/Processing → true, status Cancelled, CompletedAt set, webhook fired`
- `Cancel_Done/Failed/Cancelled → false, unchanged`
- `Cancel_unknown → null`
- `Retry_Cancelled_job accepted` (both url/ → Reingesting and stored-media → Queued)
- `Cancelled job in DB → pipeline early exit` (covered by status check unit test if feasible, else by predicate)

`dotnet build` + NUnit green before MR.
