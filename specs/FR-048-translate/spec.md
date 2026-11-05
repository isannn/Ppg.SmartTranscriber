# FR-048 — Transcript translate

Status: Approved (autonomous)
Jira: ST-49

POST /api/v1/jobs/{id}/translate {to:"en", regenerate?} → job-level
translation via the LLM chain. Stored in new column
translation_<to> or single `translation` + `translation_lang` (migration
0012). Cached — regenerate=true re-runs. CLI api translate <id> --to en.
