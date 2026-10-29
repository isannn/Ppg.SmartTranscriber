# FR-001 — Implementation Plan

Status: Approved

## Approach

Re-create the solution from scratch under `src/` rather than patching the
old projects in place — the old `Ppg.SmartTranscriber.*` projects are
deleted after their reusable code (Blazor pages, MinIO service logic,
entity shapes) is ported into the new layout.

Order:

1. `src/SmartTranscriber.Core` — entities, enums, ports, options, DTOs,
   service interfaces + skeleton services.
2. `src/SmartTranscriber.Infrastructure` — empty-but-registered
   implementations: `SmartTranscriberDbMigrator` (DbUp, scripts land in
   FR-002), `MinioStorageService` (ported from old MinioService, real
   wiring in FR-003), `SidecarTranscriber`/`WhisperCppTranscriber` stubs,
   `TranscriptionWorker` skeleton.
3. `src/SmartTranscriber.Api` — minimal-API/controllers skeleton:
   `GET /api/v1/health` real, other controllers return `501` placeholders;
   DI: options from `smarttranscriber.json`, services, hosted worker;
   Blazor Server components ported from old Web project (pages render
   against service interfaces — real pages in FR-008); bind 127.0.0.1:8460.
4. `src/SmartTranscriber.Cli` — `System.CommandLine` (or plain arg
   parsing) command skeleton: `transcribe`, `api upload|jobs|job|transcript|search`.
5. `tests/SmartTranscriber.Tests` — NUnit skeleton + a couple of
   smoke tests (options binding, enum parse).
6. Root files: `smarttranscriber.json`, `AGENTS.md`, `README.md`,
   updated `.gitignore`; delete legacy projects/files per FR-001.9.
7. Compose stub deferred to FR-010 (only `stt-svc`/minio placeholders).

## Affected paths

- new: `src/**`, `tests/**`, `db/`, `tools/stt_svc/` (stub),
  `smarttranscriber.json`, `AGENTS.md`
- deleted: `Ppg.SmartTranscriber.*/` (6 old projects), root
  `TranscriptView.razor.cs`, `daemon.json`, `node_modules/`, `.cursor/`,
  `.vs/`, `export-cert.ps1`, old `docker-compose.yml` (replaced FR-010)
- renamed: `Ppg.SmartTranscriber.sln` → `SmartTranscriber.sln`

## Dependencies / risks

- .NET 9 SDK 9.0.308 present on host.
- Ports 8460–8463 verified free via netstat (2026-10-28).
- Existing GitLab repo `main/smarttranscriber`, Jira `ST`, card — done.
- Git history of old code stays on `main`/`dev`; deletion is a commit,
  not a rewrite.
