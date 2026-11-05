# FR-059 — Chunked long-file transcription

Status: Approved (autonomous)
Jira: ST-60

## What
Files longer than Chunking:MaxChunkSeconds are split into wav
chunks (ffmpeg segment muxer), each transcribed sequentially via
the normal chain, segments merged with time offsets into one job.

## Changes
- `ChunkingOptions` (Enabled, MaxChunkSeconds=1800).
- `AudioChunker` (Infrastructure/Media): ffmpeg -f segment →
  chunk_%03d.wav in temp dir.
- Pipeline: if probed duration > threshold → chunk → per-chunk
  TranscribeAsync → offset segments → single TranscriptResult.
  Progress mapped across chunks; cleanup in finally.

## Acceptance
- A >30min file produces one job with continuous timestamps.
- Short files unchanged.
