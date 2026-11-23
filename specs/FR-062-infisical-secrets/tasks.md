# FR-062 tasks — Infisical secrets bootstrap

- **task-7** `InfisicalSecretsClient` + `InfisicalOptions` + NUnit tests
  (stub `HttpMessageHandler`: happy path parse, `__`→`:` normalization,
  non-200 → throws, bad JSON → throws, empty list → empty dict)
  — branch `tasks/task-7-infisical-client`
- **task-8** `AddInfisicalSecretsAsync` extension + `Program.cs` wiring
  (after `AddEnvironmentVariables`, before DI build; fail-fast on error)
  — branch `tasks/task-8-infisical-wiring`
- **task-9** Ops + docs: upload secrets to `/smarttranscriber`, add
  `INFISICAL_*` to `.env`/`.env.example`, strip migrated keys, watchdog
  restart, live positive + negative smoke, `identities.md`/INFRA notes
  — branch `tasks/task-9-infisical-ops`
