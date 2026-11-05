# FR-051 — SignalR live progress push

Status: Approved (autonomous)
Jira: ST-52

## What
Job progress/status currently reaches the UI only on page load (DB
polling writes every ~3s, pages never refresh). Push updates live
over SignalR instead.

## Design
- Core port `IJobProgressSink.Report(jobId, progress?, status?)` +
  `GetSnapshot` for late joiners; `NullJobProgressSink` default.
- Api: `ProgressHub` at `/hubs/progress` (groups `job-{id}`),
  `SignalRJobProgressSink` singleton (latest-state dict +
  IHubContext broadcast, payload `JobUpdateDto`).
- `TranscriptionPipeline` reports progress band + status changes.
- Jobs.razor + Transcript.razor open a `HubConnection`, join active
  jobs, apply `jobUpdate` via record `with` → StateHasChanged.
- Hub path is outside `/api/v1` bearer middleware (same-origin UI).

## Acceptance
- Opening /jobs during transcription shows live progress bars and
  status flips to Done/Failed without refresh.
- API/CLI unaffected (Null sink when Api absent).
