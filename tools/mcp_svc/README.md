# mcp_svc — SmartTranscriber MCP server

stdio JSON-RPC MCP server (Python stdlib only) that exposes the
SmartTranscriber REST API as MCP tools for Devin / TeleCoder /
other MCP clients.

## Run

```bash
python tools/mcp_svc/server.py
```

## Config (env)

| Var | Default | Purpose |
|---|---|---|
| `SMARTTRANSCRIBER_URL` | `http://127.0.0.1:8460` | API base URL |
| `SMARTTRANSCRIBER_TOKEN` | — | bearer token (`Auth:Token`) |

## Tools

`st_upload`, `st_transcribe_url`, `st_job`, `st_jobs`, `st_transcript`,
`st_summary`, `st_translate`, `st_search`, `st_stats`, `st_voiceprints`,
`st_deliveries`, `st_clip` — thin wrappers over `/api/v1/*`.

## MCP client config example

```json
{
  "mcpServers": {
    "smarttranscriber": {
      "command": "python",
      "args": ["D:/projects/ppg/Ppg.SmartTranscriber/tools/mcp_svc/server.py"],
      "env": { "SMARTTRANSCRIBER_TOKEN": "..." }
    }
  }
}
```
