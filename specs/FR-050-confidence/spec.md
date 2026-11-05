# FR-050 — Low-confidence segment highlighting

Status: Approved (autonomous)
Jira: ST-51

## What
faster-whisper exposes per-word `probability` and per-segment
`avg_logprob`; GigaAM/onnx_asr does not expose confidence.

Plumb a 0..1 confidence through the whole chain and highlight
low-confidence segments in the transcript UI.

## Changes
- `tools/stt_svc/core.py`: `Word.confidence`, `Block.confidence`;
  faster-whisper words get `w.probability` (fallback exp(avg_logprob));
  blocks aggregate the mean of word confidences.
- `tools/stt_svc/main.py`: emit `confidence` per segment.
- `TranscriptSegment.Confidence`, migration `0013_segment_confidence.sql`.
- `SidecarTranscriber` parses `confidence`; `SegmentRepository` persists.
- `SegmentDto.Confidence`; `Transcript.razor` marks rows with
  confidence < 0.6 via `table-warning` + legend.

## Acceptance
- New whisper jobs show highlighted rows for uncertain segments.
- GigaAM jobs (no confidence) render as before (NULL → no highlight).
- Old jobs unaffected.
