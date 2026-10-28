# FR-001 — Bootstrap & Clean-Architecture Restructure

Status: Draft
Jira: ST-1
Card: `ppg_projects/SmartTranscriber` (`features.md` FR-001)

## What / Why

`Ppg.SmartTranscriber` today is a half-finished scaffold: .NET 8, five
loosely-coupled projects, controllers calling `DbContext` directly, magic
status strings, a worker that shells out to a nonexistent `whisper` binary,
a double-nested `Interfaces` namespace, DTOs living in the Data project,
and stray files at the repo root. DB/storage run on hard-coded compose
ports (5432/8080/3000) that all collide with existing containers.

Goal: rebuild the solution on clean architecture and the established Ppg
house pattern so it becomes a real product — a REST API service + Blazor
UI in one container and a CLI usable by bots — while subsequent FRs
(002..010) add persistence, pipeline, endpoints, UI and ops on top.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-001.1 | Solution restructured to `src/` + `tests/` + `db/` + `tools/` + `specs/` + `docs/` layout |
| FR-001.2 | All projects on **.NET 9**; old `Ppg.SmartTranscriber.*` namespaces replaced by `SmartTranscriber.*` |
| FR-001.3 | Layering: `Core` (domain entities, enums, ports/interfaces, application services, DTOs) → `Infrastructure` (Dapper repos, DbUp, MinIO, STT clients) → `Api`/`Web`/`Cli` (delivery). Dependency direction strictly inward |
| FR-001.4 | `JobStatus` enum replaces magic strings; entities get `Id`, `ObjectName`, `Status`, `CreatedAt`, `ErrorMessage`, `Segments` |
| FR-001.5 | Single merged host app: `SmartTranscriber.Api` serves `/api/v1/*` AND hosts the Blazor UI on **:8460** (in-proc service injection, same as InterviewGrocker) |
| FR-001.6 | `SmartTranscriber.Cli` console: command routing skeleton (`transcribe`, `api …`) — real logic lands in FR-005/FR-007 |
| FR-001.7 | Non-secret config in `smarttranscriber.json` (ports, MinIO, STT endpoints); secrets only via env `SMARTTRANSCRIBER__*` (Npgsql k=v format) |
| FR-001.8 | Port block **8460–8463** (verified free): 8460 app, 8461 stt-svc, 8462 MinIO console, 8463 MinIO S3 API |
| FR-001.9 | Cleanup: drop `node_modules`, stray `TranscriptView.razor.cs` at root, `daemon.json`, `.cursor`, `.vs`, old `.csproj`s after migration |
| FR-001.10 | `AGENTS.md` project constitution + rewritten `README.md` |
| FR-001.11 | `dotnet build` green on the new solution; legacy solution files removed |

## Non-functional requirements

- All docs/comments/log/console output in English
- Nullable reference types, file-scoped namespaces, records for models
- No secrets in repo; existing git history kept (`main`/`dev` already on GitLab)

## Out of scope (later FRs)

- Actual DB schema/repositories (FR-002), MinIO impl (FR-003), STT sidecar
  (FR-004), pipeline worker (FR-005), endpoint logic (FR-006), CLI logic
  (FR-007), real Blazor pages (FR-008), FTS (FR-009), compose (FR-010).

FR-001 delivers compilable skeletons + DI wiring + options binding so each
subsequent FR fills a slot without re-plumbing.

## Acceptance criteria

1. `dotnet build SmartTranscriber.sln` succeeds, zero warnings about missing refs
2. `dotnet run --project src/SmartTranscriber.Api` starts, binds 127.0.0.1:8460, `/api/v1/health` → 200, `/` renders Blazor shell
3. `dotnet run --project src/SmartTranscriber.Cli -- --help` lists commands
4. No tracked file references `Ppg.SmartTranscriber.` namespace
5. `smarttranscriber.json` exists with the agreed port block

## Target architecture (design reference for all FRs)

```
src/
  SmartTranscriber.Core/            (no deps)
    Domain/      TranscriptionJob, TranscriptSegment, JobStatus
    Ports/       IJobRepository, ISegmentRepository, IStorageService,
                 ITranscriber, ITranscriptRenderer
    Services/    JobService, TranscriptService, SearchService
    Options/     SmartTranscriberOptions, MinioOptions, SttOptions
    Dto/         JobDto, SegmentDto, TranscriptResult
  SmartTranscriber.Infrastructure/  (deps: Core, Dapper, Npgsql, Minio, DbUp)
    Persistence/ DbUp migrator (db/), *Repository (Dapper)
    Storage/     MinioStorageService
    Stt/         SidecarTranscriber (http://127.0.0.1:8461),
                 WhisperCppTranscriber (configurable binary path)
    Transcription/ JobQueue (channel), TranscriptionWorker
  SmartTranscriber.Api/             (deps: Core, Infrastructure)
    Controllers/v1/  FilesController, JobsController, SegmentsController,
                     SearchController, HealthController
    Hosted/          worker registered as BackgroundService
    Web/             Blazor components moved here (routes /, /jobs, /jobs/{id})
  SmartTranscriber.Cli/             (deps: Core, Infrastructure)
    commands: transcribe (standalone), api (REST client)
tools/
  stt_svc/   FastAPI wrapper over TranscribiroNova core.py
db/          DbUp SQL scripts
```

Design patterns: Ports & Adapters (hexagonal), Strategy (`ITranscriber`
chain), Repository, Options pattern, DI throughout, Queue-based worker.
