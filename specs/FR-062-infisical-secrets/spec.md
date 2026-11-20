# FR-062: Infisical secrets bootstrap

- Status: Draft
- Jira: ST-63
- Branch: `features/FR-062-infisical-secrets`

## Problem

SmartTranscriber secrets (DB connection string, LLM key, MinIO keys, proxies)
live as duplicated copies: `.env` for the watchdog-run host process and
`docker-compose.yml` substitutions for containers. Copies drift, there is no
audit trail, and every agent working on the repo can read real values.

Infisical (`http://127.0.0.1:8464`) is already deployed with project `ppg`,
a `/smarttranscriber` secret folder, and a read-only machine identity
`smarttranscriber` (Token Auth; token stored in
`D:\projects\selfhosted\infisical\st_identity.env`).

## Scope

- `SmartTranscriber.Api` host fetches secrets from Infisical at startup and
  injects them into `IConfiguration` before DI is built.
- Out of scope: `stt_svc` sidecar (`HF_TOKEN`), `SmartTranscriber.Cli`
  (`SMARTTRANSCRIBER_API_TOKEN`), `docker-compose`/rustfs wiring (`S3_*`,
  `DEVIN_OPENAI_API_KEY`), other PPG services.

## Requirements

### Functional

- **FR-062.1** Bootstrap env vars read from the process environment only:
  `INFISICAL_TOKEN`, `INFISICAL_PROJECT_ID` (required to activate);
  `INFISICAL_URL` (default `http://127.0.0.1:8464`),
  `INFISICAL_ENV` (default `prod`),
  `INFISICAL_SECRET_PATH` (default `/smarttranscriber`) — optional.
- **FR-062.2** When `INFISICAL_TOKEN` + `INFISICAL_PROJECT_ID` are present,
  the host calls `GET /api/v3/secrets/raw?workspaceId=<pid>&environment=<env>
  &secretPath=<path>` with `Authorization: Bearer <token>`, timeout 10 s.
- **FR-062.3** Returned `secretKey`/`secretValue` pairs are injected via
  `AddInMemoryCollection` **after** `AddEnvironmentVariables()` so Infisical
  values win over local env. Keys keep env-var naming
  (`SMARTTRANSCRIBER__Minio__AccessKey`); `__` is normalized to `:`.
- **FR-062.4** Absent bootstrap vars → loader is a no-op (local dev keeps
  working on `.env` alone). Fetch failure (timeout, HTTP error, bad JSON) →
  `LogWarning` and continue with env/JSON config — Infisical is NOT a hard
  dependency (fail-open).
- **FR-062.5** Startup log states whether Infisical secrets were loaded and
  how many keys arrived. Secret values are never logged.
- **FR-062.6** Loader lives in `SmartTranscriber.Infrastructure`
  (external HTTP concern); wiring is one call in `Program.cs`
  (`await builder.Configuration.AddInfisicalAsync(...)`).
- **FR-062.7** Infisical `/smarttranscriber` is populated with the current
  `SMARTTRANSCRIBER__*` values (plus `S3_ACCESS_KEY`, `S3_SECRET_KEY`,
  `DEVIN_OPENAI_API_KEY`, `HF_TOKEN` for completeness). `.env` then keeps
  only bootstrap + non-secret keys; secret entries may stay as fallback
  but are no longer authoritative.

### Non-functional

- No new NuGet dependency — plain `HttpClient` + `System.Text.Json`.
- Nullable reference types, file-scoped namespace, Allman braces.
- No secret values in source, specs, logs, or commit messages.

## Acceptance criteria

- `dotnet build` clean; NUnit green including new loader tests
  (stub `HttpMessageHandler`: parse + `__`→`:` normalization,
  HTTP failure → empty result + no throw).
- Live smoke: API on :8460 starts with only `INFISICAL_*` env (no
  `SMARTTRANSCRIBER__*` in env) and works — proving secrets came from
  Infisical.
- Live negative smoke: API still starts with a bad/unreachable
  `INFISICAL_URL` when local env secrets are present (fail-open).
- `readme`/`AGENTS.md` or spec documents the `INFISICAL_*` contract and
  identity scope; `selfhosted/infisical/identities.md` notes that the
  `smarttranscriber` identity is now consumed by the API host.
