# FR-028 — Merge speakers

Status: Draft
Jira: ST-29

## What / Why

Diarization sometimes splits one person into SPEAKER_00/01. "Merge"
collapses source label into an existing one — same bulk UPDATE as
rename, but a distinct endpoint/verb so consumers express intent.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-028.1 | `POST /api/v1/jobs/{id}/speakers/merge {from,to}` → `{merged:N}` — bulk UPDATE, same engine as rename |
| FR-028.2 | CLI `api merge <id> <from> <to>` |
| FR-028.3 | UI: rename bar already covers it — label updated to "Rename / merge speaker" |
