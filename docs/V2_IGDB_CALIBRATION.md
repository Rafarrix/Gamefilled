# IGDB calibration guardrails — Gamefilled V2

Status: **human-assisted quality calibration** of search, discovery and special-category results.

## What must NOT change automatically

The existing post-PAP V2 experience already contains tuned query behavior.
Prior automated edits to these criteria produced visibly wrong game results.

Before any changes to:
- `Infrastructure/IgdbClient.cs` search/discover/top-rated/trending/recent rules,
- `Pages/api/IgdbSearch*`, `Pages/api/IgdbDiscover*`,
- specialized query categories, filters, ranking, sorting or exclusion rules,
- new "For You" candidate-generation criteria,

capture actual before/after outputs for a representative set of games and
search terms, using equivalent inputs. **Human calibration means evaluating
whether the results are satisfactory for the intended experience**, spotting
false positives, missing games, inconsistent editions/DLCs and poor ordering.
The owner helps define expected outcomes and judge borderline cases; this
is NOT a requirement to manually approve every implementation detail.

Developers may prepare, test and propose fixes independently. Validate the
quality of meaningful query/ranking changes with representative examples
and obtain feedback on the user-facing result before treating calibration
as complete. Keep regression fixtures for expected examples. A passing CI
build proves correctness of execution, not relevance of search results.

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
5. Compare each result to the intended user experience, using the owner's
   judgement where relevance is subjective or inconsistent.
6. Refine until satisfactory; add regression cases for desired results and
   keep the final query behavior documented.
