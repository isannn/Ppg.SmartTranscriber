# FR-064 tasks

- **task-1** — core cancellation plumbing:
  `JobStatus.Cancelled`, `JobCancellationRegistry`, DI registration,
  `JobService.CancelJobAsync` + retry accepts `Cancelled`,
  `IngestUrlAsync` cts wiring.
- **task-2** — pipeline + fetcher:
  registry in `TranscriptionPipeline` (early-exit, linked token, catch
  branch), `YtDlpMediaFetcher` process kill on cancel.
- **task-3** — surface area:
  `POST /jobs/{id}/cancel`, CLI `api cancel`, `Jobs.razor` Cancel/Retry
  buttons + terminal-set updates (`Transcript.razor` if needed), readme.
- **task-4** — `JobCancelTests` + full `dotnet build`/NUnit green.

All on `features/FR-064-job-cancel` (small enough for direct commits;
tasks are checkpoints, not branches).
