# Transcription pipeline (ST-5)

Status: Implemented (retroactive spec — delivered inside the FR-001 approved architecture)

Implemented: `TranscriberChain` (first-available strategy: SidecarTranscriber → WhisperCppTranscriber), `JobQueue` (channel), `TranscriptionWorker` hosted service (queue drain + SKIP LOCKED DB sweep), `TranscriptionPipeline` (download → transcribe → persist → Done/Failed; resets to Pending on shutdown).
