ALTER TABLE media_frames ADD COLUMN IF NOT EXISTS ocr_text TEXT;
ALTER TABLE media_frames DROP COLUMN IF EXISTS ocr_vector;
ALTER TABLE media_frames ADD COLUMN ocr_vector tsvector
    GENERATED ALWAYS AS (to_tsvector('russian', coalesce(ocr_text, ''))) STORED;
CREATE INDEX IF NOT EXISTS idx_media_frames_ocr ON media_frames USING gin (ocr_vector);
