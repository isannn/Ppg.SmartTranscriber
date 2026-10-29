# FR-001 — Tasks

| task | Description | Status |
|------|-------------|--------|
| task-1 | Create `src/SmartTranscriber.Core`: domain entities, `JobStatus`, ports, options, DTOs, service interfaces/skeletons | DONE |
| task-2 | Create `src/SmartTranscriber.Infrastructure`: DI extension, migrator skeleton, MinIO storage (ported), transcriber stubs, worker skeleton | DONE |
| task-3 | Create `src/SmartTranscriber.Api`: merged API+Blazor host on :8460, health endpoint, placeholder controllers, ported Blazor shell | DONE |
| task-4 | Create `src/SmartTranscriber.Cli`: command skeleton + help | DONE |
| task-5 | `tests/SmartTranscriber.Tests` NUnit skeleton + smoke tests | DONE |
| task-6 | Root config/docs: `smarttranscriber.json`, `AGENTS.md`, `README.md`, `.gitignore`; delete legacy projects/stray files; new sln; green build | DONE |
