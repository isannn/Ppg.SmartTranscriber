# Full-text search (ST-9)

Status: Implemented (retroactive spec — delivered inside the FR-001 approved architecture)

Implemented: `text_vector` generated column `to_tsvector('russian')` + GIN index; `russian` config chosen for Cyrillic stemming + Latin passthrough; `plainto_tsquery` + `ts_headline` snippets; endpoint `/api/v1/search` + `/search` page.
