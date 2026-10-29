# Domain + persistence (ST-2)

Status: Implemented (retroactive spec — delivered inside the FR-001 approved architecture)

Implemented: `SmartTranscriber.Core/Domain` (TranscriptionJob, TranscriptSegment, JobStatus enum), Dapper repositories (`Persistence/JobRepository`, `SegmentRepository`), DbUp migrations `db/Migrations/0001_init.sql` + `0002_fts_russian.sql` on `postgres-central-srv:5433/db_smarttranscriber`. Writes pass status as explicit text; reads rely on native enum parsing.
