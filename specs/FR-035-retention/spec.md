# FR-035 — Retention cleanup

Status: Approved (autonomous)
Jira: ST-36

RetentionWorker sweeps every Retention:SweepMinutes: jobs in Done/Failed
older than Retention:MaxAgeDays get deleted — row (cascades segments/
frames) + S3 media object + frames/{jobId}/*. Off when MaxAgeDays=0.
