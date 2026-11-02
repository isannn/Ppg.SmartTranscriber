# FR-039 — Bulk job operations

Status: Approved (autonomous)
Jira: ST-40

POST /api/v1/jobs/bulk {action:"delete"|"retry", ids:[uuid...]} →
{results:[{id,ok}]} — delete removes job+media+frames (deleteFile),
retry re-queues Failed jobs (409→ok:false for others).
CLI: api bulk delete|retry <id...>
