# FR-034 plan

1. `db/Migrations/0007_frame_ocr.sql` — ocr_text + ocr_vector generated
2. `IKeyFrameExtractor` — change ExtractAsync signature? No: MediaFrame gets `OcrText` field; `IFrameRepository` gains `UpdateOcrAsync(jobId, frameNo, text)`
3. `Ports/IOcr.cs` — `OcrAsync(imagePath) -> string`; `TesseractOcr` in Infra/Ocr (IsAvailable via `where tesseract`)
4. Pipeline: `ExtractFramesAsync` → then `OcrFramesAsync(job, frames, ct)` best-effort
5. `SegmentRepository.SearchAsync` — UNION second SELECT over media_frames with ts_rank on ocr_vector; `SearchHitDto.Type` ("segment"/"frame")
