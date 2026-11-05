CREATE TABLE IF NOT EXISTS webhook_deliveries (
    id bigserial PRIMARY KEY,
    job_id uuid NOT NULL REFERENCES transcription_jobs(id) ON DELETE CASCADE,
    url text NOT NULL,
    event text NOT NULL,
    status_code integer NULL,
    success boolean NOT NULL,
    error text NULL,
    duration_ms integer NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_webhook_deliveries_job
    ON webhook_deliveries(job_id);
