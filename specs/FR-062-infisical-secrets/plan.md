# FR-062 plan — Infisical secrets bootstrap

## Approach

Single bootstrap step in `SmartTranscriber.Api/Program.cs` right after
`builder.Configuration.AddEnvironmentVariables()`:

```csharp
await builder.Configuration.AddInfisicalSecretsAsync(logger, ct);
```

The extension (Infrastructure) reads `INFISICAL_*` env vars, fetches the raw
secrets list, normalizes `__` → `:` and calls `AddInMemoryCollection` — so
Infisical keys land in the same `SmartTranscriber:*` config tree the app
already binds, with higher priority than env vars.

Fail-fast: any fetch/parse error when `INFISICAL_TOKEN` +
`INFISICAL_PROJECT_ID` are set → `InvalidOperationException` (wraps the real
cause) → host exits during startup, watchdog sees a clean failure instead of
a process running on stale secrets. Missing bootstrap vars → return `null`,
no-op.

## New files

| File | Purpose |
|---|---|
| `src/SmartTranscriber.Infrastructure/Infisical/InfisicalOptions.cs` | `record` { Url, Token, ProjectId, Environment, SecretPath }; `FromEnvironment()` → `null` unless token+projectId set |
| `src/SmartTranscriber.Infrastructure/Infisical/InfisicalSecretsClient.cs` | `internal sealed`; ctor takes `HttpMessageHandler` (null → real client); `FetchAsync` → `GET {url}/api/v3/secrets/raw?workspaceId=…&environment=…&secretPath=…`, `Authorization: Bearer`, 10 s `CancellationTokenSource` timeout, parses `{secrets:[{secretKey,secretValue}]}`, returns `Dictionary` with `__`→`:` |
| `src/SmartTranscriber.Infrastructure/Infisical/InfisicalConfigurationExtensions.cs` | `public static async Task<int?> AddInfisicalSecretsAsync(this IConfigurationBuilder builder, ILogger? logger = null, CancellationToken ct = default)`; logs `Infisical secrets loaded: N keys from {path} ({env})`, never values |
| `tests/SmartTranscriber.Tests/InfisicalSecretsClientTests.cs` | NUnit + stub `HttpMessageHandler` |

## Touched files

- `src/SmartTranscriber.Api/Program.cs` — one `await` call + log line.
- `SmartTranscriber.Tests.csproj` — only if the test project doesn't already
  cover new file placement (it globs `*.cs` — no change expected).

## Sequence

1. `InfisicalSecretsClient` + tests (stub handler: happy path, `__`
   normalization, non-200 → throws, bad JSON → throws, empty list → empty
   dict).
2. `InfisicalOptions.FromEnvironment` + `AddInfisicalSecretsAsync`.
3. `Program.cs` wiring.
4. Ops (after green tests): upload current `SMARTTRANSCRIBER__*` + `S3_*` +
   `HF_TOKEN` + `DEVIN_OPENAI_API_KEY` values from `.env` to Infisical
   `/smarttranscriber` via admin token; add `INFISICAL_TOKEN` /
   `INFISICAL_PROJECT_ID` (+ `INFISICAL_URL` etc. defaults) to `.env`;
   strip `SMARTTRANSCRIBER__*` entries from `.env` (fail-fast means no
   fallback needed; `.env.example` updated to show only `INFISICAL_*`).
5. Live smoke: watchdog restart → API healthy on :8460 with secrets only
   from Infisical; negative: bad `INFISICAL_URL` → host refuses to start.
6. Docs: `.env.example`, `selfhosted/infisical/identities.md` note,
   INFRA.md §2 line already covers Infisical — add "consumers" column note.

## Risks

- Secret keys must keep exact env-var names (`SMARTTRANSCRIBER__Minio__AccessKey`)
  — a typo in Infisical = missing config value → startup-time failure is loud
  (good under fail-fast).
- `INFISICAL_TOKEN` stays in `.env` — acceptable: it is scoped, revocable,
  audited; documented as the root of trust.
