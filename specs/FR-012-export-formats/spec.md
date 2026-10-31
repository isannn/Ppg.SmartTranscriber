# FR-012 — Export formats SRT/VTT/JSON

Status: Draft
Jira: ST-12

## What / Why

The transcript should be downloadable as standard subtitle files so it
can be attached to the source video or fed into other tools.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-012.1 | `GET /api/v1/jobs/{id}/transcript?format=txt|json|srt|vtt` — srt/vtt added to existing endpoint |
| FR-012.2 | SRT: numbered cues `HH:MM:SS,mmm --> HH:MM:SS,mmm`, speaker prefix `SPEAKER_XX: text` |
| FR-012.3 | VTT: `WEBVTT` header, cues `HH:MM:SS.mmm --> HH:MM:SS.mmm`, `<v SPEAKER_XX>` voice tags |
| FR-012.4 | Correct Content-Type (`application/x-subrip`, `text/vtt`) + `Content-Disposition` filename `<original>.<ext>` |
| FR-012.5 | UI download links for txt/srt/vtt on transcript page; CLI `--format` passthrough (already works) |
| FR-012.6 | Unit tests for SRT/VTT rendering (timestamps, speakers, escaping) |

## Acceptance criteria

1. `curl .../transcript?format=srt` returns a valid SRT with Content-Disposition
2. `curl .../transcript?format=vtt` starts with `WEBVTT` and has `<v` voice tags
3. NUnit green
