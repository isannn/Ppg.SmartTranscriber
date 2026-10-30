# FR-014 — YouTube URL ingest

Status: Draft
Jira: ST-14

## What / Why

Create a transcription job directly from a YouTube (or other yt-dlp
supported) URL — media fetched via `yt-dlp`, uploaded to S3, job
enqueued automatically.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-014.1 | `IMediaFetcher` port + `YtDlpMediaFetcher` (spawns `py -3.11 -m yt_dlp`, `--proxy` from `YtDlp:Proxy` option or `TG_PROXY` env, `-f bestaudio/best`, `--print after_move:filepath`) |
| FR-014.2 | New `JobStatus.Downloading` — worker claims only `Pending`, so fetch-time jobs are invisible to it |
| FR-014.3 | `POST /api/v1/jobs/from-url` `{url}` → 202 `{id}`; background: fetch → S3 upload → `Pending` (+queue); failure → `Failed` + error_message |
| FR-014.4 | CLI `api url <link>`; UI: URL input on Upload page |
| FR-014.5 | yt-dlp timeout 600s (`YtDlp:TimeoutSeconds`), no schema changes |

## Acceptance criteria

1. POST a YouTube URL → job appears as Downloading → becomes Done with transcript
2. CLI `api url <link>` works; bad URL → Failed with clear error
