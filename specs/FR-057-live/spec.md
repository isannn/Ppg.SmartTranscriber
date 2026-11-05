# FR-057 — Live transcription sessions

Status: Approved (autonomous)
Jira: ST-58

## What
Stream PCM audio into a session; a hosted worker incrementally
transcribes new audio (overlap window) and appends segments to a
real job — progress/status flow through the existing SignalR sink.

## API
- `POST /api/v1/live` {language?, model?, hotwords?, diarize?}
  → {sessionId, jobId}
- `POST /api/v1/live/{id}/chunk` — raw s16le/16kHz/mono PCM bytes
- `POST /api/v1/live/{id}/finish` — finalize job (Done + webhook)
- `GET /api/v1/live/{id}` — session status

## Design
`LiveSessionManager` (in-memory sessions + temp PCM files, 32kB/s),
`LiveTranscriptionWorker` ticks every Live:TickSeconds: slice
[covered-overlap, end] → wav via ffmpeg → sidecar /transcribe
(diarize off) → dedup overlap → offset segments → persist →
progressSink. Job row created Processing directly (never queued).
Media not uploaded to MinIO in v1 (ObjectName=live://...).

## Acceptance
- Chunks produce transcript segments within ~2 ticks; UI job page
  shows live progress; finish marks Done and fires webhooks.
