# FR-024 — Summary options

Status: Draft
Jira: ST-25

## What / Why

POST /summary always regenerates with a fixed prompt+model. Give callers
control: custom prompt, model name (OpenRouter), explicit regenerate.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-024.1 | `SummaryRequest {model?, prompt?}` — POST /summary accepts it; POST regenerates only when `?regenerate=true` or no cached summary (404→generate) |
| FR-024.2 | `ISummarizer.SummarizeAsync(text, request?, ct)` — OpenAiSummarizer uses request.Model/Prompt overrides; AcpSummarizer uses Prompt (devin has no per-call model) |
| FR-024.3 | CLI `api summarize <id> [--prompt P] [--model M] [--regenerate]` |

## Acceptance criteria

1. `POST /summary {prompt:"..."}` returns text shaped by the custom prompt
2. second POST without regenerate → cached, no LLM call
