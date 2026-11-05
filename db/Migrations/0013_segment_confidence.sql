ALTER TABLE transcript_segments
    ADD COLUMN IF NOT EXISTS confidence double precision NULL;
