CREATE TABLE IF NOT EXISTS speaker_voiceprints (
    id bigserial PRIMARY KEY,
    name text NOT NULL UNIQUE,
    embedding text NOT NULL,
    source_job_id uuid NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);
