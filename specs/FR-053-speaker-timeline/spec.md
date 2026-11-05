# FR-053 — Speaker timeline

Status: Approved (autonomous)
Jira: ST-54

## What
A horizontal "who speaks when" strip above the transcript table:
consecutive same-speaker segments merge into colored blocks
positioned by % of job duration, with a speaker legend.

## Changes
UI-only (Transcript.razor): derive turns from loaded segments,
render absolutely-positioned colored blocks + legend. No backend.

## Acceptance
- Diarized jobs show a readable speaker timeline; clicking a block
  seeks the player to that turn's start.
- Jobs without speakers render nothing.
