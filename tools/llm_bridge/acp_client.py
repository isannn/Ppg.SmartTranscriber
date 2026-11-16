"""Minimal one-shot ACP JSON-RPC client (stdlib only).

Spawns an ACP-compatible subprocess, runs the handshake
(initialize -> authenticate -> session/new), sends one session/prompt,
aggregates session/update text notifications until the prompt response
arrives, then always terminates the subprocess — on success, error,
timeout, or cancellation.

Safety rules:
- any inbound agent->client request (tool calls, permission prompts) is
  answered with a JSON-RPC error and fails the extraction;
- message bodies are never logged or embedded in exceptions;
- the caller supplies argv + cwd; this module never resolves credentials.
"""

from __future__ import annotations

import json
import subprocess
import threading
import time


class AcpError(Exception):
    """Sanitized protocol/subprocess failure (never carries message bodies)."""


class AcpProcess:
    """One ACP stdio subprocess: handshake, one prompt turn, cleanup."""

    def __init__(self, argv, cwd=None, timeout_sec=120.0):
        self._argv = list(argv)
        self._cwd = cwd
        self._timeout = float(timeout_sec)
        self._proc = None
        self._next_id = 0
        self._responses = {}
        self._chunks = []
        self._inbound = []
        self._reader_error = None
        self._cond = threading.Condition()
        self._closed = False

    def start(self):
        self._proc = subprocess.Popen(
            self._argv,
            cwd=self._cwd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            bufsize=1,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        threading.Thread(target=self._read_loop, daemon=True).start()
        return self

    # ---------------- transport ----------------

    def _read_loop(self):
        try:
            for line in self._proc.stdout:
                line = line.strip()
                if not line:
                    continue
                try:
                    msg = json.loads(line)
                except json.JSONDecodeError:
                    continue
                with self._cond:
                    if "id" in msg and ("result" in msg or "error" in msg):
                        self._responses[msg["id"]] = msg
                    elif msg.get("method") == "session/update":
                        self._collect_update(msg)
                    elif "method" in msg and "id" in msg:
                        # agent -> client request: deny it, then fail the turn
                        self._inbound.append(msg)
                        self._deny(msg)
                    self._cond.notify_all()
        except Exception as exc:  # stream death surfaces as AcpError
            with self._cond:
                self._reader_error = exc
                self._cond.notify_all()

    def _collect_update(self, msg):
        update = msg.get("params", {}).get("update", {})
        if update.get("sessionUpdate") == "agent_message_chunk":
            content = update.get("content", {})
            if content.get("type") == "text" and content.get("text"):
                self._chunks.append(content["text"])

    def _deny(self, msg):
        try:
            self._send({
                "jsonrpc": "2.0",
                "id": msg.get("id"),
                "error": {"code": -32601, "message": "denied"},
            })
        except Exception:
            pass

    def _send(self, msg):
        self._proc.stdin.write(json.dumps(msg) + "\n")
        self._proc.stdin.flush()

    def _request(self, method, params):
        self._next_id += 1
        rid = self._next_id
        self._send({
            "jsonrpc": "2.0",
            "id": rid,
            "method": method,
            "params": params,
        })
        return rid

    def _wait_response(self, rid, deadline):
        with self._cond:
            while True:
                if self._inbound:
                    raise AcpError(
                        "agent issued an inbound request; tool/permission access denied")
                if rid in self._responses:
                    msg = self._responses[rid]
                    if "error" in msg:
                        raise AcpError("JSON-RPC error response")
                    return msg.get("result")
                if self._reader_error is not None:
                    raise AcpError("agent subprocess stream failed")
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise AcpError("timeout waiting for agent response")
                self._cond.wait(remaining)

    # ---------------- protocol ----------------

    def extract(self, prompt_text, auth_method=None, api_key=None, cwd=None):
        """Full handshake + one prompt turn; returns aggregated assistant text."""
        deadline = time.monotonic() + self._timeout
        try:
            init = self._wait_response(self._request("initialize", {
                "protocolVersion": 1,
                "clientCapabilities": {},
            }), deadline)

            # devin acp advertises authMethods when it needs authenticate —
            # pick the first id and pass the api_key via _meta
            methods = (init or {}).get("authMethods") or []
            method_id = auth_method or (methods[0].get("id") if methods else None)
            if method_id is not None:
                params = {"methodId": method_id}
                if api_key is not None:
                    params["_meta"] = {"api_key": api_key}
                self._wait_response(self._request("authenticate", params), deadline)

            result = self._wait_response(self._request("session/new", {
                "cwd": cwd or self._cwd or ".",
                "mcpServers": [],
            }), deadline)
            session_id = (result or {}).get("sessionId")
            if not session_id:
                raise AcpError("session/new returned no session id")

            self._wait_response(self._request("session/prompt", {
                "sessionId": session_id,
                "prompt": [{"type": "text", "text": prompt_text}],
            }), deadline)

            return "".join(self._chunks)
        finally:
            self.close()

    # ---------------- cleanup ----------------

    def close(self):
        if self._closed:
            return
        self._closed = True
        proc = self._proc
        if proc is None:
            return
        try:
            proc.stdin.close()
        except Exception:
            pass
        try:
            proc.terminate()
            proc.wait(timeout=5)
        except Exception:
            try:
                proc.kill()
                proc.wait(timeout=5)
            except Exception:
                pass
        try:
            proc.stdout.close()
        except Exception:
            pass


def run_extraction(argv, prompt_text, cwd=None, timeout_sec=120.0,
                   auth_method=None, api_key=None):
    """Spawn argv, run one extraction prompt, always clean up."""
    proc = AcpProcess(argv, cwd=cwd, timeout_sec=timeout_sec)
    proc.start()
    return proc.extract(prompt_text, auth_method=auth_method,
                        api_key=api_key, cwd=cwd)
