# FR-030 — Auto summary on Done

Status: Draft
Jira: ST-31

When `Summary:AutoGenerate=true` (env `SMARTTRANSCRIBER__Summary__AutoGenerate`),
the pipeline runs SummarizerChain after Done and stores job.summary —
best-effort, failure logged only. Off by default (LLM calls aren't free).
