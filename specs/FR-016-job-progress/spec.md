# FR-016 — Job progress

Status: Draft
Jira: ST-16

## What / Why

A multi-hour transcription gives no feedback. Expose honest phase-based
progress (no fake percentages) on the job: API field + UI bar.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-016.1 | `progress smallint 0-100` column on jobs (migration 0004), entity/DTO/repo plumbing |
| FR-016.2 | Pipeline milestones: claimed=10, media downloaded=25, transcribing=30, transcription done=90, segments persisted=95, Done=100 (Downloading stays 0-10 via ingest) |
| FR-016.3 | Progress visible in `GET /api/v1/jobs/{id}` + Jobs list UI (bar + %) |
| FR-016.4 | Failed keeps last reached value |

## Acceptance criteria

1. New job shows rising progress through phases, ends at 100 on Done
2. Jobs.razor renders a progress bar for unfinished jobs
