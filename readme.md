# SmartTranscriber

Local, self-hosted audio/video transcription service. Single-user, no auth,
fully offline (GigaAM v3 / Whisper models run locally).

Two shapes from one codebase:

1. **API service + Web UI** — ASP.NET Core REST endpoints + Blazor UI on
   `http://127.0.0.1:8460`, deployable via `docker compose up`
   (app + stt-svc + minio).
2. **CLI** — `smarttranscriber` console for bots and scripts:
   `transcribe <file>` standalone + `api` subcommands against the service.

## Stack

| Area | Choice |
|------|--------|
| Backend | .NET 9 ASP.NET Core + Blazor Server (merged host) |
| Persistence | PostgreSQL `db_smarttranscriber` on `postgres-central-srv` :5433, Dapper + DbUp |
| Media storage | MinIO (S3-compatible) |
| STT | python FastAPI sidecar `tools/stt_svc` — GigaAM v3 / faster-whisper + pyannote diarization; whisper.cpp offline fallback |
| CLI | `SmartTranscriber.Cli` |

## Ports

| Port | Service |
|------|---------|
| 8460 | API + Blazor UI |
| 8461 | stt-svc (FastAPI) |
| 8462 | MinIO console |
| 8463 | MinIO S3 API |

## API endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/health` | health probe |
| POST | `/api/v1/files` | upload media (multipart) → 202 + job |
| GET | `/api/v1/jobs` | list jobs (`?status=&limit=&offset=`) |
| GET | `/api/v1/jobs/{id}` | job detail |
| DELETE | `/api/v1/jobs/{id}` | delete job (`?deleteFile=true`) |
| GET | `/api/v1/jobs/{id}/segments` | timestamped segments |
| PUT | `/api/v1/segments/{id}/speaker` | rename speaker |
| GET | `/api/v1/jobs/{id}/transcript?format=txt|json` | rendered transcript |
| GET | `/api/v1/search?q=&limit=` | full-text search over segments |

Swagger UI: `http://127.0.0.1:8460/swagger`. Machine-readable spec (no auth): `GET /api/v1/openapi.json`.

## CLI

```powershell
# standalone transcription (stt sidecar -> whisper.cpp fallback)
smarttranscriber transcribe "I:\records\interview.mp4" -o out.txt

# against a running service
smarttranscriber api upload "I:\records\interview.mp4"
smarttranscriber api jobs --status Done
smarttranscriber api transcript <job-id> -o transcript.txt
smarttranscriber api search "kubernetes"

# apply DB migrations
smarttranscriber db migrate
```

Publish single-file for bots:
`dotnet publish src/SmartTranscriber.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`

## Run

Prerequisites: `SMARTTRANSCRIBER__ConnectionString` env
(Npgsql `key=value` format) pointing at `db_smarttranscriber` on
`postgres-central-srv` :5433.

```powershell
# everything containerized (app + stt-svc + minio)
docker compose up -d --build

# or local dev: sidecar in the TranscribiroNova venv (GPU), app via dotnet
ppg_projects\TranscribiroNova\.venv\Scripts\python.exe tools\stt_svc\main.py
dotnet run --project src/SmartTranscriber.Api
```

Config: `smarttranscriber.json` at repo root (ports, endpoints — no
secrets); secrets via env only.

## Layout

```
src/SmartTranscriber.Core/          domain entities, ports, app services
src/SmartTranscriber.Infrastructure/ Dapper repos, DbUp, MinIO, STT clients
src/SmartTranscriber.Api/           REST /api/v1/* + Blazor UI (:8460)
src/SmartTranscriber.Cli/           console client / standalone transcriber
db/Migrations/                      DbUp SQL scripts
tools/stt_svc/                      FastAPI STT sidecar
tests/                              NUnit
specs/                              spec-driven design docs
```
