"""llm-bridge: loopback HTTP front over `devin.exe acp` (one-shot prompts).

GET  /health -> {"status":"ok"}
POST /chat   {"prompt": "..."} -> {"text": "..."} — spawns a devin acp
             subprocess per request, denies any inbound tool request.
"""
from __future__ import annotations
import json, os, sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

from acp_client import run_extraction, AcpError

HOST = "127.0.0.1"
PORT = int(os.environ.get("LLM_BRIDGE_PORT", "8464"))
DEVIN_ARGV = os.environ.get("DEVIN_ACP_ARGV", "devin.exe acp").split()
TIMEOUT = float(os.environ.get("LLM_BRIDGE_TIMEOUT", "600"))


def _load_api_key():
    key = os.environ.get("DEVIN_API_KEY")
    if key:
        return key
    creds = os.path.join(os.environ.get("APPDATA", ""), "devin", "credentials.toml")
    try:
        import tomllib
        with open(creds, "rb") as f:
            data = tomllib.load(f)
        return data.get("windsurf_api_key") or data.get("api_key")
    except Exception:
        return None


API_KEY = _load_api_key()

class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def _json(self, code, obj):
        body = json.dumps(obj).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        self._json(200 if self.path == "/health" else 404,
                   {"status": "ok", "service": "llm-bridge"} if self.path == "/health" else {"error": "not found"})

    def _read_body(self):
        if "chunked" in self.headers.get("Transfer-Encoding", "").lower():
            buf = b""
            while True:
                line = self.rfile.readline(65536).strip()
                try:
                    n = int(line.split(b";")[0], 16)
                except ValueError:
                    break
                if n == 0:
                    while True:
                        t = self.rfile.readline(65536)
                        if t in (b"\r\n", b"\n", b""):
                            break
                    break
                buf += self.rfile.read(n)
                self.rfile.read(2)
            return buf
        length = int(self.headers.get("Content-Length", "0"))
        return self.rfile.read(length) if length else b""

    def do_POST(self):
        if self.path != "/chat":
            return self._json(404, {"error": "not found"})
        try:
            payload = json.loads(self._read_body() or b"{}")
            prompt = payload.get("prompt", "")
            if not prompt:
                return self._json(400, {"error": "prompt required"})
            text = run_extraction(DEVIN_ARGV, prompt, cwd=os.getcwd(),
                                  timeout_sec=TIMEOUT, api_key=API_KEY)
            self._json(200, {"text": text})
        except AcpError as e:
            self._json(502, {"error": str(e)})
        except Exception as e:
            self._json(500, {"error": type(e).__name__})

    def log_message(self, *a):
        pass

if __name__ == "__main__":
    ThreadingHTTPServer((HOST, PORT), Handler).serve_forever()
