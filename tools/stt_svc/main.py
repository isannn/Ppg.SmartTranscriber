"""stt-svc: FastAPI speech-to-text sidecar for SmartTranscriber.

POST /transcribe (multipart file) -> JSON {segments, model, language, duration}
GET  /health -> {"status": "ok"}

Reuses the TranscribiroNova pipeline (core.py): ffmpeg -> ASR
(GigaAM v3 / faster-whisper) -> pyannote diarization -> speaker blocks.
"""

from __future__ import annotations

import os
import tempfile

import uvicorn
from fastapi import FastAPI, File, HTTPException, Query, UploadFile

import core

app = FastAPI(title="stt-svc", version="1.0.0")

MEDIA_EXTENSIONS = core.MEDIA_EXTENSIONS

# in-flight job progress: job_id -> {"frac": float, "stage": str}
_PROGRESS: dict[str, dict] = {}


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "service": "stt-svc", "default_model": core.DEFAULT_MODEL}


@app.get("/progress/{job_id}")
def progress(job_id: str) -> dict:
    return _PROGRESS.get(job_id, {"frac": None, "stage": "unknown"})


@app.post("/transcribe")
async def transcribe(
    file: UploadFile = File(...),
    model: str = Query("", description="ASR model (gigaam-* or whisper size)"),
    language: str = Query("", description="Language code, e.g. ru/en"),
    diarize: bool = Query(True),
    num_speakers: int | None = Query(None),
    job_id: str = Query("", description="optional progress key — poll GET /progress/{job_id}"),
    hotwords: str = Query("", description="comma-separated glossary terms to bias ASR"),
) -> dict:
    filename = file.filename or "media.bin"
    ext = os.path.splitext(filename)[1].lower()
    if ext and ext not in MEDIA_EXTENSIONS:
        raise HTTPException(400, f"unsupported extension: {ext}")

    with tempfile.TemporaryDirectory() as tmp:
        media_path = os.path.join(tmp, "input" + (ext or ".bin"))
        with open(media_path, "wb") as f:
            f.write(await file.read())

        try:
            wav_path = core.extract_audio(media_path, tmp)
        except Exception as e:
            raise HTTPException(422, f"audio extraction failed: {e}") from e

        if job_id:
            _PROGRESS[job_id] = {"frac": 0.0, "stage": "transcribing"}
        try:
            words, meta = core.transcribe_words(
                wav_path,
                model_size=model or core.DEFAULT_MODEL,
                language=language or None,
                hotwords=hotwords or None,
                progress=lambda f: _PROGRESS.update(
                    {job_id: {"frac": f, "stage": "transcribing"}}) if job_id else None,
            )
        finally:
            if job_id:
                _PROGRESS.pop(job_id, None)

        turns: list[tuple[float, float, str]] = []
        if diarize:
            try:
                from diarization import diarize as run_diarization

                turns = run_diarization(
                    wav_path, os.environ.get("HF_TOKEN"), "auto", num_speakers
                )
            except Exception as e:
                print(f"diarization skipped: {e}")

        if turns:
            core.assign_speakers(words, turns)
        blocks = core.words_to_blocks(words)

    return {
        "segments": [
            {
                "start": round(b.start, 3),
                "end": round(b.end, 3),
                "text": b.text.strip(),
                "speaker": b.speaker,
            }
            for b in blocks
        ],
        "model": meta.get("model"),
        "language": meta.get("language"),
        "duration": meta.get("duration"),
        "diarization": bool(turns),
    }


if __name__ == "__main__":
    host = os.environ.get("STT_HOST", "127.0.0.1")
    port = int(os.environ.get("STT_PORT", "8461"))
    uvicorn.run(app, host=host, port=port, log_level="info")
