# FR-056 — MCP server

Status: Approved (autonomous)
Jira: ST-57

## What
Expose SmartTranscriber as MCP tools so Devin/TeleCoder/other MCP
clients can transcribe, poll, search and fetch results natively.

## Design
`tools/mcp_svc/server.py` — stdio JSON-RPC MCP server, Python stdlib
only (zero deps). Wraps the REST API:

- Env: `SMARTTRANSCRIBER_URL` (default http://127.0.0.1:8460),
  `SMARTTRANSCRIBER_TOKEN` (bearer).
- Protocol: newline-delimited JSON-RPC — initialize, ping,
  tools/list, tools/call; notifications ignored.
- Tools: upload, transcribe_url, job, jobs, transcript, summary,
  translate, search, stats, voiceprints, clip.

## Acceptance
- `python tools/mcp_svc/server.py` speaks MCP on stdio; tools call
  the live API with bearer auth.
