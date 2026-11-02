# FR-043 — Job tags + list filter

Status: Approved (autonomous)
Jira: ST-44

`tags` comma-string on jobs (migration 0011). PUT /jobs/{id}/tags
{tags:"a,b"}; GET /jobs?tag=x filters list (substring on normalized
`,a,b,` storage). CLI: api tag <id> <tags>, api jobs --tag x.
