# FR-026 — API bearer auth

Status: Draft
Jira: ST-27

## What / Why

/api/v1/* is open on loopback. Before exposing the port beyond
localhost, add a bearer token (env-configured; empty = disabled).

## Functional requirements

| # | Requirement |
|---|-------------|
| FR-026.1 | Middleware on /api/v1/* (except /api/v1/health): 401 unless `Authorization: Bearer <token>` or `?token=` (media tags can't set headers) |
| FR-026.2 | `Auth:Token` option, env `SMARTTRANSCRIBER__Auth__Token`; empty = auth off |
| FR-026.3 | CLI sends the token from `SMARTTRANSCRIBER_API_TOKEN` env; UI media src gets ?token= when configured |
