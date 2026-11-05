# FR-055 — Voiceprints (cross-job speaker recognition)

Status: Approved (autonomous)
Jira: ST-56

## What
Recognize the same person across jobs: per-speaker embeddings
(pyannote wespeaker via sidecar) are stored in Postgres; on each
diarized job, new speaker clusters are cosine-matched against known
voiceprints and segments are renamed to the canonical name.

## Changes
- Sidecar: `diarization.extract_voiceprints` — sliding-window
  Inference embeddings aggregated per speaker turn (max-overlap,
  duration-weighted mean, L2-normalized); `/transcribe?voiceprints=`
  opt-in, best-effort.
- DB `speaker_voiceprints` (name UNIQUE, embedding JSON, source job).
- `IVoiceprintRepository`, `VoiceprintMatcher` (cosine ≥ threshold),
  `ISegmentRepository.RemapSpeakersAsync`.
- Pipeline `ApplyVoiceprintsAsync` after segment insert: match →
  remap labels → upsert embeddings. Options `Voiceprints:Enabled`,
  `MatchThreshold` (0.55).
- `GET /api/v1/voiceprints` + CLI `api voiceprints`.

## Acceptance
- Second job with the same voice gets canonical speaker names.
- Unknown voices get unique names; failures never block transcription.
