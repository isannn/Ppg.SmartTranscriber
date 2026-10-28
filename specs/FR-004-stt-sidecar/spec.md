# STT sidecar (ST-4)

Status: Implemented (retroactive spec — delivered inside the FR-001 approved architecture)

Implemented: `tools/stt_svc` FastAPI — `GET /health`, `POST /transcribe` (multipart file + model/language/diarize/num_speakers) → `{segments,model,language,duration,diarization}`. Vendored TranscribiroNova `core.py`+`diarization.py` (GigaAM v3 / faster-whisper / pyannote). Dockerfile is CPU-only; GPU path = run in TranscribiroNova venv.
