# FR-013 — LLM summary

Status: Draft
Jira: ST-13

## What / Why

Generate a summary of the transcript via a local OpenAI-compatible LLM
(llama.cpp gpt-oss :11435, Ollama :11434 fallback — same client).

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-013.1 | `ISummarizer` port + `OpenAiSummarizer` (chat completions, `SmartTranscriber:Llm` options: BaseUrl/Model/Timeout/MaxTranscriptChars) |
| FR-013.2 | `summary` column on jobs (migration 0003); `POST /api/v1/jobs/{id}/summary` generates+stores+returns, `GET` returns stored |
| FR-013.3 | Transcript page: "Generate summary" button + rendered result |
| FR-013.4 | CLI `api summarize <id>` |
| FR-013.5 | Transcript truncated to MaxTranscriptChars before prompting; clear error when LLM unreachable |

## Acceptance criteria

1. POST summary on the test job returns text and persists (GET returns it)
2. UI button works; CLI `api summarize` prints it

## Amendment (2026-10-29, user): LLM provider chain

- Primary: `tools/llm_bridge` (loopback :8464) — `devin.exe acp`
  (swe-2-max) via ACP JSON-RPC, vendored client from InterviewGrocker,
  inbound tool requests denied, subprocess per request.
- Fallback: OpenRouter free models via `OpenAiSummarizer`
  (`Llm:BaseUrl`/`Model`/`ApiKey` — env `OPENROUTER_API_KEY`).
- `SummarizerChain`: bridge → openrouter (same strategy as TranscriberChain).
- C#: `AcpSummarizer` + `SummarizerChain` replace direct ISummarizer binding.
