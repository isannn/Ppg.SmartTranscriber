#!/usr/bin/env python3
"""MCP stdio server exposing the SmartTranscriber REST API as tools.

Zero dependencies — newline-delimited JSON-RPC 2.0 on stdin/stdout.

Config (env):
    SMARTTRANSCRIBER_URL    default http://127.0.0.1:8460
    SMARTTRANSCRIBER_TOKEN  bearer token (optional)

Run:  python tools/mcp_svc/server.py
"""

from __future__ import annotations

import json
import mimetypes
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE_URL = os.environ.get("SMARTTRANSCRIBER_URL", "http://127.0.0.1:8460").rstrip("/")
TOKEN = os.environ.get("SMARTTRANSCRIBER_TOKEN", "")
PROTOCOL_VERSION = "2025-03-26"
SERVER_INFO = {"name": "smarttranscriber", "version": "1.0.0"}


# ---------------------------------------------------------------- REST ----

def _request(method: str, path: str, body: bytes | None = None,
             content_type: str | None = None) -> tuple[int, str]:
    url = BASE_URL + path
    req = urllib.request.Request(url, data=body, method=method)
    if content_type:
        req.add_header("Content-Type", content_type)
    if TOKEN:
        req.add_header("Authorization", f"Bearer {TOKEN}")
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            return resp.status, resp.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")
    except Exception as e:
        return 0, json.dumps({"error": str(e)})


def api(method: str, path: str, payload: dict | None = None) -> str:
    body = json.dumps(payload).encode() if payload is not None else None
    status, text = _request(method, path, body, "application/json" if body else None)
    return _fmt(status, text)


def _fmt(status: int, text: str) -> str:
    try:
        pretty = json.dumps(json.loads(text), ensure_ascii=False, indent=2)
    except Exception:
        pretty = text
    return f"HTTP {status}\n{pretty}"


def _qs(params: dict) -> str:
    clean = {k: v for k, v in params.items() if v not in (None, "")}
    return "?" + urllib.parse.urlencode(clean) if clean else ""


def upload_file(path: str, fields: dict) -> str:
    if not os.path.isfile(path):
        return json.dumps({"error": f"file not found: {path}"})
    boundary = uuid.uuid4().hex
    ctype = mimetypes.guess_type(path)[0] or "application/octet-stream"
    parts = []
    for name, value in fields.items():
        if value in (None, ""):
            continue
        parts.append(
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n".encode()
        )
    fname = os.path.basename(path)
    parts.append(
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{fname}\"\r\n"
        f"Content-Type: {ctype}\r\n\r\n".encode()
    )
    with open(path, "rb") as f:
        data = f.read()
    parts.append(data)
    parts.append(f"\r\n--{boundary}--\r\n".encode())
    body = b"".join(parts)
    status, text = _request("POST", "/api/v1/files", body,
                            f"multipart/form-data; boundary={boundary}")
    return _fmt(status, text)


def download_clip(job_id: str, frm: float, to: float, out: str) -> str:
    status, data = _request("GET", f"/api/v1/jobs/{job_id}/clip?from={frm}&to={to}")
    # _request decodes text; for binary we need bytes — redo raw
    if status != 200:
        return _fmt(status, data)
    # binary path: fetch again as bytes
    url = f"{BASE_URL}/api/v1/jobs/{job_id}/clip?from={frm}&to={to}"
    req = urllib.request.Request(url, method="GET")
    if TOKEN:
        req.add_header("Authorization", f"Bearer {TOKEN}")
    with urllib.request.urlopen(req, timeout=300) as resp:
        blob = resp.read()
    with open(out, "wb") as f:
        f.write(blob)
    return json.dumps({"saved": out, "bytes": len(blob)})


# ---------------------------------------------------------------- tools ---

def t_upload(a):
    return upload_file(a["path"], {
        "callback_url": a.get("callback_url"),
        "hotwords": a.get("hotwords"),
        "priority": a.get("priority"),
    })


def t_transcribe_url(a):
    return api("POST", "/api/v1/jobs/from-url", {
        "url": a["url"],
        "callbackUrl": a.get("callback_url"),
        "hotwords": a.get("hotwords"),
        "priority": a.get("priority"),
    })


def t_job(a):
    return api("GET", f"/api/v1/jobs/{a['id']}")


def t_jobs(a):
    return api("GET", "/api/v1/jobs" + _qs({
        "status": a.get("status"), "tag": a.get("tag"),
        "limit": a.get("limit", 50), "offset": a.get("offset", 0),
    }))


def t_transcript(a):
    return api("GET", f"/api/v1/jobs/{a['id']}/transcript" + _qs({
        "format": a.get("format", "txt"),
    }))


def t_summary(a):
    method = "POST" if a.get("regenerate") else "GET"
    return api(method, f"/api/v1/jobs/{a['id']}/summary")


def t_translate(a):
    return api("POST", f"/api/v1/jobs/{a['id']}/translate", {
        "to": a.get("to", "en"), "regenerate": a.get("regenerate", False),
    })


def t_search(a):
    return api("GET", "/api/v1/search" + _qs({
        "q": a["query"], "limit": a.get("limit", 20),
        "jobId": a.get("job_id"), "speaker": a.get("speaker"),
        "model": a.get("model"),
    }))


def t_stats(a):
    return api("GET", "/api/v1/stats")


def t_voiceprints(a):
    return api("GET", "/api/v1/voiceprints")


def t_deliveries(a):
    return api("GET", "/api/v1/webhook-deliveries" + _qs({
        "jobId": a.get("job_id"), "limit": a.get("limit", 50),
    }))


def t_clip(a):
    try:
        return download_clip(a["id"], float(a["from"]), float(a["to"]), a["output"])
    except Exception as e:
        return json.dumps({"error": str(e)})


S = {"type": "string"}
I = {"type": "integer"}
B = {"type": "boolean"}
F = {"type": "number"}

TOOLS = [
    {"name": "st_upload", "description": "Upload a local media file and create a transcription job",
     "inputSchema": {"type": "object", "required": ["path"], "properties": {
         "path": S, "callback_url": S, "hotwords": S, "priority": I}}},
    {"name": "st_transcribe_url", "description": "Create a transcription job from a media URL (YouTube etc.)",
     "inputSchema": {"type": "object", "required": ["url"], "properties": {
         "url": S, "callback_url": S, "hotwords": S, "priority": I}}},
    {"name": "st_job", "description": "Get one transcription job by id (status, progress, model, summary)",
     "inputSchema": {"type": "object", "required": ["id"], "properties": {"id": S}}},
    {"name": "st_jobs", "description": "List transcription jobs",
     "inputSchema": {"type": "object", "properties": {
         "status": S, "tag": S, "limit": I, "offset": I}}},
    {"name": "st_transcript", "description": "Get a job's transcript (format: txt|srt|vtt|json)",
     "inputSchema": {"type": "object", "required": ["id"], "properties": {
         "id": S, "format": S}}},
    {"name": "st_summary", "description": "Get cached LLM summary, or regenerate with regenerate=true",
     "inputSchema": {"type": "object", "required": ["id"], "properties": {
         "id": S, "regenerate": B}}},
    {"name": "st_translate", "description": "LLM-translate a job's transcript (to: en|ru|...)",
     "inputSchema": {"type": "object", "required": ["id"], "properties": {
         "id": S, "to": S, "regenerate": B}}},
    {"name": "st_search", "description": "Full-text search across transcripts and OCR'd frames",
     "inputSchema": {"type": "object", "required": ["query"], "properties": {
         "query": S, "limit": I, "job_id": S, "speaker": S, "model": S}}},
    {"name": "st_stats", "description": "Aggregate stats: jobs/day, durations, models",
     "inputSchema": {"type": "object", "properties": {}}},
    {"name": "st_voiceprints", "description": "List known speaker voiceprints",
     "inputSchema": {"type": "object", "properties": {}}},
    {"name": "st_deliveries", "description": "Webhook delivery log (status codes, errors)",
     "inputSchema": {"type": "object", "properties": {
         "job_id": S, "limit": I}}},
    {"name": "st_clip", "description": "Cut an mp4 clip [from,to] seconds of a job's media to a local file",
     "inputSchema": {"type": "object", "required": ["id", "from", "to", "output"], "properties": {
         "id": S, "from": F, "to": F, "output": S}}},
]

HANDLERS = {
    "st_upload": t_upload,
    "st_transcribe_url": t_transcribe_url,
    "st_job": t_job,
    "st_jobs": t_jobs,
    "st_transcript": t_transcript,
    "st_summary": t_summary,
    "st_translate": t_translate,
    "st_search": t_search,
    "st_stats": t_stats,
    "st_voiceprints": t_voiceprints,
    "st_deliveries": t_deliveries,
    "st_clip": t_clip,
}


# ------------------------------------------------------------ JSON-RPC ----

def _result(req_id, result):
    return {"jsonrpc": "2.0", "id": req_id, "result": result}


def _error(req_id, code, message):
    return {"jsonrpc": "2.0", "id": req_id,
            "error": {"code": code, "message": message}}


def handle(msg: dict) -> dict | None:
    method = msg.get("method", "")
    req_id = msg.get("id")
    is_notification = "id" not in msg

    if method == "initialize":
        params = msg.get("params") or {}
        return _result(req_id, {
            "protocolVersion": params.get("protocolVersion", PROTOCOL_VERSION),
            "capabilities": {"tools": {}},
            "serverInfo": SERVER_INFO,
        })
    if method == "ping":
        return None if is_notification else _result(req_id, {})
    if method == "tools/list":
        return _result(req_id, {"tools": TOOLS})
    if method == "tools/call":
        params = msg.get("params") or {}
        name = params.get("name", "")
        args = params.get("arguments") or {}
        handler = HANDLERS.get(name)
        if handler is None:
            return _error(req_id, -32602, f"unknown tool: {name}")
        try:
            text = handler(args)
            return _result(req_id, {"content": [{"type": "text", "text": text}]})
        except Exception as e:
            return _result(req_id, {
                "content": [{"type": "text", "text": json.dumps({"error": str(e)})}],
                "isError": True,
            })
    # notifications & unknown methods
    if is_notification or method.startswith("notifications/"):
        return None
    return _error(req_id, -32601, f"method not found: {method}")


def main() -> None:
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            msg = json.loads(line)
        except json.JSONDecodeError:
            continue
        if not isinstance(msg, dict):
            continue
        resp = handle(msg)
        if resp is not None:
            sys.stdout.write(json.dumps(resp, ensure_ascii=False) + "\n")
            sys.stdout.flush()


if __name__ == "__main__":
    main()
