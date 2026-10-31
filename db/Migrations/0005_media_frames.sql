CREATE TABLE IF NOT EXISTS media_frames (
    id           BIGSERIAL PRIMARY KEY,
    job_id       UUID NOT NULL REFERENCES transcription_jobs(id) ON DELETE CASCADE,
    frame_no     INT  NOT NULL,
    time_seconds REAL NOT NULL,
    object_name  TEXT NOT NULL,
    UNIQUE (job_id, frame_no)
);
CREATE INDEX IF NOT EXISTS idx_media_frames_job ON media_frames(job_id);
