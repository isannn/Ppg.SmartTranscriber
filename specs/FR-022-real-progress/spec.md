# FR-022 — Real transcription progress

Status: Draft
Jira: ST-23

## What / Why

Phase milestones (10/25/30/90/95) jump coarsely. The sidecar's core
already reports fraction (seg.end/duration) — surface it live.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-022.1 | stt_svc `POST /transcribe?job_id=` tracks in-memory `{frac,stage}`; `GET /progress/{job_id}` reads it; cleaned up on finish |
| FR-022.2 | `ITranscriber.TranscribeAsync(…, IProgress<double>?)` — sidecar polls `/progress/{job_id}` each 3s while POST in-flight |
| FR-022.3 | Pipeline maps frac→30-89% band, writes job.progress throttled ≥3s |
| FR-022.4 | whisper.cpp/other transcribers ignore the param; progress is best-effort |
