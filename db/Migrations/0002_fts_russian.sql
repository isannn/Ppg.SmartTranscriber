-- 0002_fts_russian.sql — switch FTS to the 'russian' config.
-- 'simple' has no stemming (запрос != запроса); 'russian' stems Cyrillic
-- and leaves Latin tech terms unstemmed (exact-match friendly).

ALTER TABLE transcript_segments DROP COLUMN IF EXISTS text_vector;

ALTER TABLE transcript_segments ADD COLUMN text_vector tsvector
    GENERATED ALWAYS AS (to_tsvector('russian', coalesce(text, ''))) STORED;

CREATE INDEX IF NOT EXISTS ix_transcript_segments_fts
    ON transcript_segments USING gin (text_vector);
