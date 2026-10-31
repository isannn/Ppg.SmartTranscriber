# FR-023 — whisper.cpp fallback: removed

Status: Draft
Jira: ST-24

## Decision

Remove `WhisperCppTranscriber` — it was never provisioned on the host
(no binary, no model, IsAvailableAsync always false) and the sidecar
already carries faster-whisper as its own second engine. Dead code out.

## Changes

- Delete `Stt/WhisperCppTranscriber.cs` + DI registration
- Drop `Stt:WhisperCppPath`/`WhisperModelPath` options + config keys
