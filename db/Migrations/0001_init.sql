-- 0001_init.sql — initial schema for SmartTranscriber

CREATE TABLE IF NOT EXISTS transcription_jobs
(
    id                  uuid PRIMARY KEY,
    object_name         text NOT NULL,
    original_file_name  text NOT NULL DEFAULT '',
    status              text NOT NULL DEFAULT 'Pending',
    created_at          timestamptz NOT NULL DEFAULT now(),
    completed_at        timestamptz NULL,
    error_message       text NULL,
    model               text NULL,
    language            text NULL,
    duration_seconds    double precision NULL
);

CREATE INDEX IF NOT EXISTS ix_transcription_jobs_status_created
    ON transcription_jobs (status, created_at DESC);

CREATE TABLE IF NOT EXISTS transcript_segments
(
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    job_id       uuid NOT NULL REFERENCES transcription_jobs (id) ON DELETE CASCADE,
    start_time   double precision NOT NULL,
    end_time     double precision NOT NULL,
    speaker      text NULL,
    text         text NOT NULL,
    text_vector  tsvector GENERATED ALWAYS AS (to_tsvector('simple', coalesce(text, ''))) STORED
);

CREATE INDEX IF NOT EXISTS ix_transcript_segments_job
    ON transcript_segments (job_id, start_time);

CREATE INDEX IF NOT EXISTS ix_transcript_segments_fts
    ON transcript_segments USING gin (text_vector);
