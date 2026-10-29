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
