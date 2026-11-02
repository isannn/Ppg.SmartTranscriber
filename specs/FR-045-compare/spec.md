# FR-045 — Job transcript diff

Status: Approved (autonomous)
Jira: ST-46

GET /api/v1/jobs/compare?a=<id>&b=<id> → {jobA, jobB, rows:
[{index, aStart, bStart, aText, bText, same}]} — positional zip of
segments (up to max count). CLI: api compare <a> <b>.
