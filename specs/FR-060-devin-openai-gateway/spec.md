# FR-060 — devin-openai gateway replaces llm_bridge

Status: Approved (consolidation directive, 2026-11-20)

## Why

`tools/llm_bridge` (:8464) wraps `devin.exe acp` as `POST /chat {prompt}`
but spawns a fresh acp process per request. The shared `devin-openai`
gateway (:8465) does the same job better: one persistent acp process,
`session/new` per request, model fallback chain, Bearer auth, watchdog
supervision — and as of today it is tool-free (denies
`session/request_permission`), matching llm_bridge's deny-all posture.

## Requirements

| Req | Description |
|---|---|
| FR-060.1 | `AcpSummarizer` posts `{prompt}` to `{Llm.BridgeUrl}/generate` (response `{text, model}`) and sends `Authorization: Bearer` when `Llm.BridgeApiKey` is set |
| FR-060.2 | `LlmOptions.BridgeUrl` default → `http://127.0.0.1:8465`; new `LlmOptions.BridgeApiKey` (env `SmartTranscriber__Llm__BridgeApiKey`, never in json) |
| FR-060.3 | `docker-compose.yml` → `Llm__BridgeUrl: http://host.docker.internal:8465` + `Llm__BridgeApiKey`; `.env` gains the key |
| FR-060.4 | `tools/llm_bridge/` deleted; TelePilot `watchdog.py` loses the `llm-bridge` target |

## Non-requirements

- SummarizerChain order unchanged: `devin-acp` primary → OpenRouter fallback.
- `/health` availability probe unchanged (gateway exposes it unauthenticated).
- No changes to OpenAiSummarizer (OpenRouter fallback path).

## Acceptance

- `dotnet build` + NUnit green.
- `POST /generate` on :8465 with the key returns `{text}` for a transcript
  prompt; `IsAvailableAsync` sees `/health`.
