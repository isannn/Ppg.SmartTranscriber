# FR-054 — Media metadata (ffprobe)

Status: Approved (autonomous)
Jira: ST-55

## What
Capture container/stream metadata during transcription: duration,
format, video/audio codecs, resolution, bitrate — stored on the job
and surfaced in API/UI.

## Changes
- `IMediaProbe` port + `MediaMetadata` record;
  `FfprobeMediaProbe` (ffprobe -show_format -show_streams).
- `transcription_jobs.media_meta text` (JSON) — migration 0015.
- Pipeline probes the downloaded file before STT; result serialized
  camelCase onto `job.MediaMeta` (persisted by later UpdateAsync).
- `MediaMetaDto` on `JobDto`; UI badges (WxH, codecs, format).
- Fix latent bug: UpdateAsync params missed Translation/TranslationLang.

## Acceptance
- Done jobs expose mediaMeta; UI shows codec/resolution badges.
- ffprobe missing/failure → job proceeds, meta null.
