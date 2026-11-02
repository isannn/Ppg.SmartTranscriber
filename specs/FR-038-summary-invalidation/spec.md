# FR-038 — Summary invalidation on segment edits

Status: Approved (autonomous)
Jira: ST-39

After FR-029 users can edit segment text — a cached job.summary then
goes stale. Clear it whenever a segment of that job is edited
(text or speaker) or bulk-renamed: next GET /summary regenerates.
Implementation: UPDATE transcription_jobs SET summary=NULL alongside
the segment UPDATE statements in SegmentRepository.
