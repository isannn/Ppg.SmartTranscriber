# FR-047 — LLM auto-tags

Status: Approved (autonomous)
Jira: ST-48

Tags:AutoGenerate (default false, like Summary:AutoGenerate) — after
Done the pipeline asks the summarizer for 3-6 short keywords and
writes them into job.Tags (comma-separated). Best-effort; empty on
failure.
