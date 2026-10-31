# FR-027 — Key frame extraction

Status: Draft
Jira: ST-28

## What / Why

Video jobs keep only audio-derived text. Extracting key frames lets
consumers (IG) see code/slides on screen. Interval+scene-detect via
ffmpeg, frames in S3, catalog in `media_frames`.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-027.1 | Migration 0005: `media_frames(job_id, frame_no, time_seconds, object_name)` |
| FR-027.2 | Post-Done (video ext only): ffmpeg scene-detect (thr .35) + fallback interval → ≤20 jpgs → S3 `frames/{jobId}/{n}.jpg` + rows; best-effort |
| FR-027.3 | `GET /api/v1/jobs/{id}/frames` → list; `GET /frames/{no}` → image/jpeg stream |
| FR-027.4 | `KeyFrames:Enabled` option; CLI `api frames <id>` |
