# File storage (ST-3)

Status: Implemented (retroactive spec — delivered inside the FR-001 approved architecture)

Implemented: `IStorageService` port + `MinioStorageService` (upload/download/delete/ensure-bucket). Note: `minio/*` images are gated on this docker proxy — compose uses `rustfs/rustfs` (S3-protocol compatible drop-in) as service `s3`.
