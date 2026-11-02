# FR-036 — Re-transcribe with another model

Status: Approved (autonomous)
Jira: ST-37

POST /api/v1/jobs/{id}/retranscribe {model?, language?, diarize?} —
reset job to Pending (clear summary/segments progress), enqueue; the
pipeline runs TranscriberChain with the requested model. Lets you
compare gigaam vs whisper on the same media.
