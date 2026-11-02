# FR-040 — Job priority queue

Status: Approved (autonomous)
Jira: ST-41

`priority int default 0` on jobs (migration 0010) — the claim sweep
orders `priority DESC, created_at`. Accepted on create/upload/url
(priority form/json param) and CLI --priority.
