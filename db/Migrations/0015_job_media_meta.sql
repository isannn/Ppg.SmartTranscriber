ALTER TABLE transcription_jobs
    ADD COLUMN IF NOT EXISTS media_meta text NULL;
