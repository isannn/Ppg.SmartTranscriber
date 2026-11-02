# FR-041 — Parallel pipeline workers

Status: Approved (autonomous)
Jira: ST-42

Worker:Concurrency (default 1) — N drain consumers on the in-memory
queue; DB claim sweep stays single (orphan recovery only).
SKIP LOCKED already makes claiming race-safe.
