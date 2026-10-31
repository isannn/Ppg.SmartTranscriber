-- 0003_job_summary.sql — cached LLM summary on jobs

ALTER TABLE transcription_jobs ADD COLUMN IF NOT EXISTS summary text NULL;
