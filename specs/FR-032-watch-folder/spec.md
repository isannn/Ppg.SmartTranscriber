# FR-032 — Watch-folder ingest

Status: Draft
Jira: ST-33

WatchFolderWorker polls `WatchFolder:Dir` (env
`SMARTTRANSCRIBER__WatchFolder__Dir`) every PollSeconds and creates a
job per new media file (mp4/webm/mkv/mov/avi/mp3/wav/m4a/ogg/flac/opus).
Done files move to `Dir/done` — that's the processed marker.
Files still being written (FileShare.None fails) are skipped.
