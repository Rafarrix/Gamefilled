# IGDB calibration guardrails — Gamefilled V2

Status: **owner-review required** for any change to search, discovery or
special-category query criteria.

## What must NOT change automatically

The existing post-PAP V2 experience already contains tuned query behavior.
Prior automated edits to these criteria produced visibly wrong game results.

Before any changes to:
- `Infrastructure/IgdbClient.cs` search/discover/top-rated/trending/recent rules,
- `Pages/api/IgdbSearch*`, `Pages/api/IgdbDiscover*`,
- specialized query categories, filters, ranking, sorting or exclusion rules,
- new "For You" candidate-generation criteria,

capture actual before/after outputs for a representative set of games and
search terms, use the same query parameters, and **review the results with
the project owner**. Only apply modifications after explicit approval.
Write regression tests for the approved examples to detect future drift.
Do not interpret a compile/CI pass as proof that ranking is good.

## Current safe scope

The library metadata migration uses **only the unchanged existing**
`IgdbClient.GetGamesByIdsAsync`: numerical `id` lookups, not search.

- Load at most 24 visible games per library page.
- Store library ownership, status, rating, reviews **only in Gamefilled SQL**.
- Use IGDB for display title, cover ID and release date only.
- Cache display metadata for 30 minutes with local memory cache.
- If IGDB is unavailable, keep library functional with fallback game IDs.
- Never accept external image URLs submitted by clients: build HTTPS URLs
  exclusively from validated IGDB `image_id` values.
- Links to `/games/{id}` are prepared, but the modernized host still
  requires game-detail route migration before those links resolve locally.
- The separate V2 Identity host remains unpublished; do not update the
  web-01 production Docker stack just because this PR passes CI.

## Human calibration workflow

1. Identify one exact query/filter change with a clear reason.
2. Prepare ~10-20 representative game searches and category cases including
   editions, DLC, remasters, older titles and missing covers.
3. Record current ordered IGDB IDs and titles for each case.
4. Run the proposed criteria against the same dataset and API credentials,
   taking account of upstream catalog changes.
5. Review the output with the project owner and only then approve the change.
6. Add tests/fixtures, merge through PR and re-run all feature regressions.
