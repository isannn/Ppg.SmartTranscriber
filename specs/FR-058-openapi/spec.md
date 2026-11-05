# FR-058 — OpenAPI document endpoint

Status: Approved (autonomous)
Jira: ST-59

## What
Swagger UI exists at /swagger but external consumers need a stable,
auth-free machine-readable spec. Expose the generated OpenAPI doc at
`GET /api/v1/openapi.json` (outside the bearer middleware) and
document it in README.

## Changes
- `OpenApiController`: GET /api/v1/openapi.json → ISwaggerProvider doc.
- Route stays outside /api/v1 auth? It IS under /api/v1 — bearer
  middleware covers /api/v1/*. Add explicit exclusion for this path
  in Program.cs (specs are not secret).
- README: link + curl example.

## Acceptance
- curl /api/v1/openapi.json returns valid OpenAPI 3.x without token.
