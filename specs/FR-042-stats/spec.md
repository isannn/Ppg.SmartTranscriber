# FR-042 — /api/v1/stats metrics endpoint

Status: Approved (autonomous)
Jira: ST-43

GET /api/v1/stats → {totals{jobs,byStatus},byModel[{model,jobs,avgDurationSeconds}],
byDay[{date,jobs}],totalDurationSeconds} — SQL aggregates for dashboards.
CLI: api stats.
