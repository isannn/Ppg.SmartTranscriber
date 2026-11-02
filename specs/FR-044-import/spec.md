# FR-044 — Import export dump

Status: Approved (autonomous)
Jira: ST-45

POST /api/v1/import accepts the FR-031 dump shape {jobs, segments,
mediaFrames} — inserts jobs that don't exist (by id), their segments
and frames; existing ids skipped. Returns {imported, skipped}.
CLI: api import <file> (reads dump JSON from file or stdin).
