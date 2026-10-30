-- 0004_job_progress.sql — phase-based progress 0-100 on jobs

ALTER TABLE transcription_jobs ADD COLUMN IF NOT EXISTS progress smallint NOT NULL DEFAULT 0;
