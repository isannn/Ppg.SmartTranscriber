# AGENTS.md — SmartTranscriber

Project constitution: mandatory rules for agents working in this
repository.

## Project

- Purpose: local self-hosted audio/video transcription — REST API +
  Blazor UI service on :8460 and a CLI usable by bots; media in MinIO,
  catalog in PostgreSQL, STT via python sidecar (GigaAM v3 /
  faster-whisper + pyannote diarization) with whisper.cpp fallback
- Stack (fixed): **.NET 9 / C#** — `SmartTranscriber.Api` (ASP.NET Core
  controllers + Blazor Server, merged host), `SmartTranscriber.Cli`
  console; PostgreSQL `db_smarttranscriber` on `postgres-central-srv`
  (:5433, Npgsql/Dapper, DbUp migrations); MinIO S3-compatible storage;
  **python sidecar** `tools/stt_svc` (FastAPI, TranscribiroNova core)
- Project card (source of truth for the FR backlog):
  `D:\projects\hlam\vscode_agentic_experiments\ppg_projects\SmartTranscriber\`
  (`features.md`, `spec/`)
- Jira: http://localhost:8080/projects/ST — feature = `New Feature` `ST-N`
- GitLab: http://gitlab.ppg.com:8929/main/smarttranscriber

## Spec-driven design (mandatory)

All work goes through specs in `specs/`:

```
specs/FR-xxx-<slug>/
├── spec.md   — what/why: description, FRs, NFRs, acceptance criteria
├── plan.md   — how: approach, affected files, dependencies
└── tasks.md  — decomposition into task-N
```

- **No code without an approved spec.md** (`Approved` status in the header).
- Phases strictly in order: spec → approve → plan → approve → tasks → implement.
- Requirements changed — fix spec.md first, then the code. Not silently.
- After the feature merges, `specs/FR-xxx-*/` stays in the repo as documentation.
- Feature-index and FR↔ST map: card `features.md`.

## Git-flow (mandatory)

- `main` — stable, `dev` — integration (default). **Never commit
  directly to `main` or `dev`.**
- Feature branch: `features/FR-xxx-<slug>` — off `dev`; spec/plan/tasks
  are committed there first.
- Task branch: `tasks/task-N-<slug>` — off the feature branch. `N` is the
  global project task counter.
- A feature without tasks — work directly on the feature branch.
- Merges only via MR and only bottom-up:
  `tasks/*` → `features/*` → `dev` → (release) `main`.
- Merge rights: the agent merges `tasks/*` → `features/*` after green
  tests; `features/*` → `dev` and `dev` → `main` — **the user reviews
  and merges** in GitLab (or explicitly approves in chat).
- **Never squash `dev` → `main`** (prefer merge commits on every MR).
- Commits: `<type>: <what was done>`, type = feat/fix/refactor/docs/test/chore;
  a Jira key (`ST-N`) in the commit body — GitLab auto-comments the card.
- Always reply with the direct MR link when creating one.

## Architecture

```
src/
  SmartTranscriber.Core/            domain entities, ports, app services, options
  SmartTranscriber.Infrastructure/  Dapper repos, DbUp, MinIO, STT clients, worker
  SmartTranscriber.Api/             REST /api/v1/* + Blazor UI (merged host :8460)
  SmartTranscriber.Cli/             transcribe (standalone) + api subcommands
db/Migrations/NNNN_*.sql            DbUp embedded scripts, roll-forward only
tools/stt_svc/                    FastAPI STT sidecar (vendored TranscribiroNova core)
tests/                            NUnit
```

- Dependency direction: Api/Cli → Infrastructure → Core. Core has no
  infrastructure references.
- All external resources behind ports in `Core/Ports/`: `IJobRepository`,
  `ISegmentRepository`, `IStorageService`, `ITranscriber`, `IJobQueue`.
- Transcriber selection: `TranscriberChain` strategy — first available
  wins (sidecar → whisper.cpp).
- Job queue: in-memory channel + `SELECT … FOR UPDATE SKIP LOCKED` sweep
  (restart-safe, single writer).

## Code

- Nullable reference types, `file-scoped` namespaces, records for models
- Docs, code comments (spec/plan/tasks, README), console output and log
  messages — **all in English**
- Style per `.editorconfig`: Allman braces, braces on every
  if/else/loop body
- Config — `smarttranscriber.json` (ports, endpoints); secrets via env
  only (`SMARTTRANSCRIBER__ConnectionString`, Npgsql key-value format,
  NOT a `postgres://` URI; `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD`,
  `HF_TOKEN` for the sidecar). FR-062: the Api host pulls
  `SMARTTRANSCRIBER__*` secrets from Infisical at startup when
  `INFISICAL_TOKEN`/`INFISICAL_PROJECT_ID` are set (fail-fast on fetch
  errors); unset → local env fallback. See
  `specs/FR-062-infisical-secrets/` and `D:\projects\selfhosted\infisical`.
- Schema changes — DbUp migrations only (`db/Migrations/`), roll-forward only
- Verification: `dotnet build` + NUnit; sidecar `python -m py_compile`
  + `/health` smoke

## Non-functional requirements (defaults)

- Local-only: binds to 127.0.0.1; before binding check the port is free
  (`netstat -ano | findstr :PORT`) — many ports are docker containers.
  Assigned: 8460 api+web, 8461 stt-svc, 8462 minio console, 8463 minio s3.
- `INFO` logging; job statuses via `JobStatus` enum only (no magic strings)
- Uploads are stored before the job row exists (object → job → enqueue);
  worker marks `Failed` with `error_message`, never loses a job silently

## Jira

- Feature = `New Feature` `ST-N`, task = subtask. The agent moves cards
  along the board with the work: spec writing → In Progress at first
  `tasks/*` branch → Done after the user merges to dev.
- Cards get remote links per feature/task MR + a comment when a change
  partially covers another card.
- Ask spec/plan approvals, merge confirmations, and "start next feature?"
  prompts via the interactive question tool, not plain-text prompts.

## Environment

- .NET SDK 9.0.308; Python 3.11.8 (`py -3.11` — hermes-venv lacks ML deps);
  ffmpeg/ffprobe 7.1.1 in PATH
- GigaAM/pyannote models in the HF cache — `HF_HUB_OFFLINE=1` works
  offline; `HF_TOKEN` needed only for the gated pyannote download
- Sidecar GPU on RTX 3080 via the TranscribiroNova venv
  (`ppg_projects/TranscribiroNova/.venv`); the docker image is CPU-only
- Long-term memory: `D:\projects\ppg\Ppg.TelePilot\memory\494633786.md`
  (also `mcp__telepilot__memory_*` tools)
