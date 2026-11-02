# FR-021 — InterviewGrocker transcript export

Status: Draft
Jira: ST-22

## What / Why

IG mines interview transcripts into its question bank via
`roots.records` folder scan (`*.txt`, media-ext kept in stem:
`name.mp4.txt`). SmartTranscriber exports finished transcripts there —
file-based, decoupled, no API coupling.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-021.1 | `Consumers:IgExportDir` option (env `SMARTTRANSCRIBER__Consumers__IgExportDir`); empty = disabled |
| FR-021.2 | On job Done, pipeline writes `RenderText` output as `<OriginalFileName>.txt` into that dir |
| FR-021.3 | Export failure is logged, never fails the job |
