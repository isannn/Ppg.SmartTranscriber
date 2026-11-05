# FR-049 — Video clip cut

Status: Approved (autonomous)
Jira: ST-50

GET /api/v1/jobs/{id}/clip?from=&to= — streams an ffmpeg-trimmed mp4
(range-enabled). ClipService: S3 media → temp → ffmpeg -ss/-to →
FileStream(DeleteOnClose). CLI: api clip <id> --from X --to Y -o file.
