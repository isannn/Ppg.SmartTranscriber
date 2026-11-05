ALTER TABLE transcription_jobs ADD COLUMN IF NOT EXISTS translation TEXT;
ALTER TABLE transcription_jobs ADD COLUMN IF NOT EXISTS translation_lang TEXT;
