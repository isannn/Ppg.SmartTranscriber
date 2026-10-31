# FR-019 — docker-compose refresh

Status: Draft
Jira: ST-20

## What / Why

Compose drifted behind the features: no healthchecks on app/stt, no
llm-bridge wiring, no GPU notes. Bring the stack up to date.

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-019.1 | Healthchecks on `app` (`/api/v1/health`) and `stt-svc` (`/health`); `depends_on: service_healthy` chain |
| FR-019.2 | `Llm__BridgeUrl` → `http://host.docker.internal:8464` (+ `extra_hosts host-gateway`); llm_bridge stays host-side (spawns `devin.exe`, needs %APPDATA% creds) — documented in compose comment + README |
| FR-019.3 | GPU note: commented `deploy.resources.reservations.devices` on stt-svc + README line (host venv is the GPU path on Windows) |
| FR-019.4 | Env passthrough: `OPENROUTER_API_KEY` → `Llm__ApiKey` fallback config |

## Acceptance criteria

1. `docker compose up -d` → all services healthy; app transcribes via compose-network stt
2. `docker compose config` renders cleanly; README ops section covers llm_bridge start on host
