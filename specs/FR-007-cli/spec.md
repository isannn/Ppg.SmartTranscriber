# CLI (ST-7)

Status: Implemented (retroactive spec — delivered inside the FR-001 approved architecture)

Implemented `SmartTranscriber.Cli`: `transcribe <file>` standalone via transcriber chain → .txt; `api upload|jobs|job|transcript|search`; `db migrate`. Config via smarttranscriber.json locator + SMARTTRANSCRIBER__* env; exit codes 0/1/2; plain stdout for bots.
