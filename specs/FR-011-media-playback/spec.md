# FR-011 — Media playback synced with transcript

Status: Draft
Jira: ST-11

## What / Why

Reviewing a transcript needs the source media alongside: the user should
play the original audio/video on the transcript page, see the currently
spoken segment highlighted, and click any segment to seek there.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-011.1 | `GET /api/v1/jobs/{id}/media` streams the source media object from S3 with HTTP Range support (seekable) |
| FR-011.2 | Content-Type resolved from the file extension (audio/*, video/*); unknown → `application/octet-stream` |
| FR-011.3 | Transcript page (`/jobs/{id}`) renders `<audio>` or `<video>` (by extension) bound to the media endpoint |
| FR-011.4 | While playing, the segment row containing `currentTime` is highlighted (data-start/data-end + JS) |
| FR-011.5 | Clicking a segment row seeks the player to its start time and plays |
| FR-011.6 | 404 when job/object missing; works for files up to multi-GB (stream, not buffer) |

## Non-functional

- No extra deps; vanilla JS in `wwwroot/media.js` + `IJSRuntime`
- 404-safe JS binding (player may be absent while segments load)

## Acceptance criteria

1. `curl -H "Range: bytes=0-1023" /api/v1/jobs/{id}/media` → 206 + correct bytes
2. Transcript page shows a working player for an existing job
3. Playback highlights the current segment; clicking a row seeks+plays
