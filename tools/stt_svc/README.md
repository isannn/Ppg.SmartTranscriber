# stt-svc — STT sidecar for SmartTranscriber

FastAPI wrapper over the TranscribiroNova pipeline
(`ppg_projects/TranscribiroNova` — `core.py` and `diarization.py` are
vendored copies; keep them in sync manually).

## API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | `{"status":"ok"}` |
| POST | `/transcribe` | multipart `file` + query `model`, `language`, `diarize`, `num_speakers` → `{segments:[{start,end,text,speaker}], model, language, duration, diarization}` |

## Run locally (GPU on this box)

```powershell
cd tools/stt_svc
python -m venv .venv
.venv\Scripts\activate
pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu121
pip install -r requirements.txt
python main.py   # http://127.0.0.1:8461
```

Requires `ffmpeg` in PATH. Diarization additionally needs `HF_TOKEN`
env with accepted licenses for `pyannote/speaker-diarization-3.1` and
`pyannote/segmentation-3.0`; without it transcription still works,
`speaker` fields stay null.

Alternatively reuse the existing venv:
`ppg_projects/TranscribiroNova/.venv/Scripts/python.exe tools/stt_svc/main.py`

## Docker

The image is CPU-only (GigaAM CTC is still ~40-60x realtime on CPU):

```bash
docker compose up stt-svc
```
