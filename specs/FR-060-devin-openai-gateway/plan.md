# FR-060 — plan

## Approach

devin-openai's `/generate {prompt, systemMessage}` → `{text, model}` is the
drop-in contract replacement for llm_bridge's `/chat {prompt}` → `{text}`.
Auth: `DEVIN_OPENAI_API_KEY` is distributed to consumers via
`SmartTranscriber__Llm__BridgeApiKey` (`.env` for host-run api, compose
interpolation for the container).

## Affected files

- `src/SmartTranscriber.Core/Options/SmartTranscriberOptions.cs` —
  `BridgeUrl` default, new `BridgeApiKey`.
- `src/SmartTranscriber.Infrastructure/Stt/AcpSummarizer.cs` — endpoint,
  Bearer header, comments.
- `docker-compose.yml`, `.env` — new url + key.
- `tools/llm_bridge/` — delete.
- `D:\projects\ppg\Ppg.TelePilot\watchdog.py` — drop `llm-bridge` Target +
  HELP_TEXT (applied on next watchdog restart; stray process killed then).
- Card `ppg_projects/SmartTranscriber/features.md` — FR-060 row.
