# FR-034 — OCR key frames into FTS

Status: Approved (autonomous mode)
Jira: ST-35

## What / Why

Video jobs keep screen content (code, slides) only as images. OCR them
and index into FTS — "find the interview where Singleton was on screen".

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-034.1 | Migration 0007: `media_frames.ocr_text` + generated `ocr_vector` (russian config) |
| FR-034.2 | `IOcr` port + `TesseractOcr` (tesseract.exe subprocess, `-l rus+eng`); absent binary = skip silently |
| FR-034.3 | Pipeline post-Done: OCR each extracted frame → `ocr_text` (best-effort) |
| FR-034.4 | `GET /api/v1/search` returns frame hits too: `{type:"frame", jobId, timeSeconds, snippet}` |

## Acceptance criteria

1. A frame containing known text produces a search hit of type "frame"
