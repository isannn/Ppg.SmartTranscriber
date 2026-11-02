# FR-029 — Segment text edit

Status: Draft
Jira: ST-30

PUT /api/v1/segments/{id}/text {text} — UPDATE transcript_segments.text
(text_vector is GENERATED — FTS updates automatically).
CLI: api segment-text <id> <text>. UI: inline edit on segment row.
