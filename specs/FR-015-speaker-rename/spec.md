# FR-015 — Global speaker rename

Status: Draft
Jira: ST-15

## What / Why

Per-segment speaker editing (FR-008) is tedious for long transcripts:
`SPEAKER_00` should be renamable to a real name in one operation.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-015.1 | `ISegmentRepository.RenameSpeakerAsync(jobId, from, to)` — single UPDATE, returns affected rows |
| FR-015.2 | `POST /api/v1/jobs/{id}/speakers/rename` `{from,to}` → `{updated}`; 404 on unknown job, 400 on empty params |
| FR-015.3 | CLI `api rename <id> <from> <to>` |
| FR-015.4 | Transcript page: rename bar (from/to inputs + button), refreshes segment list |
| FR-015.5 | Rename applies to all segments of the job; case-sensitive match |

## Acceptance criteria

1. POST rename SPEAKER_00→Иван → all SPEAKER_00 rows updated, response {updated:N}
2. CLI + UI paths work
