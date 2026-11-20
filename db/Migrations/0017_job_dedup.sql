ALTER TABLE transcription_jobs ADD COLUMN IF NOT EXISTS source_url   text NULL;
ALTER TABLE transcription_jobs ADD COLUMN IF NOT EXISTS content_hash text NULL;
ALTER TABLE transcription_jobs ADD COLUMN IF NOT EXISTS duplicate_of uuid NULL;

-- Live URL owners only: a Failed job drops out of the index so the URL
-- can be re-claimed by a resubmission.
CREATE UNIQUE INDEX IF NOT EXISTS ux_transcription_jobs_source_url
    ON transcription_jobs (source_url)
    WHERE source_url IS NOT NULL AND status <> 'Failed';

CREATE INDEX IF NOT EXISTS ix_transcription_jobs_content_hash
    ON transcription_jobs (content_hash)
    WHERE content_hash IS NOT NULL;

-- Best-effort backfill: URL jobs still waiting in Downloading keep the
-- submitted URL in original_file_name until ingest rewrites it.
UPDATE transcription_jobs
SET source_url = original_file_name
WHERE status = 'Downloading'
  AND source_url IS NULL
  AND original_file_name ~* '^https?://';
