# FR-017 — Batch ingest

Status: Draft
Jira: ST-17

## What / Why

Uploading files one by one is slow for backlog directories. Batch:
multiple files in one request (API/UI) and a directory scan (CLI).

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-017.1 | `POST /api/v1/files/batch` (multipart, N files) → one job per file → `[{id, fileName, status}]` |
| FR-017.2 | Upload page: `InputFile multiple` — creates a job per selected file, lists created ids |
| FR-017.3 | CLI `api upload` accepts multiple paths; `api upload --dir <path>` scans media extensions (*.mp3 .wav .m4a .aac .ogg .opus .flac .wma .mp4 .mkv .webm .avi .mov) |
| FR-017.4 | One bad file fails its own job creation, not the batch |

## Acceptance criteria

1. `api upload --dir <dir>` creates a job per media file, prints ids
2. Batch endpoint + UI multi-select create N jobs
