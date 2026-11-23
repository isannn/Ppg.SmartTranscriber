# FR-060 — tasks

- task-1: `LlmOptions` + `AcpSummarizer` → `/generate` + Bearer.
- task-2: `.env` + `docker-compose.yml` env wiring.
- task-3: delete `tools/llm_bridge/`; drop watchdog target; update
  features.md card.
- task-4: `dotnet build` + tests; live `/generate` smoke; restart api
  (watchdog picks up new binary) and kill the orphaned llm_bridge after
  the watchdog reloads.
