## 1. Shared copy + selectors

- [x] 1.1 `web/src/lib/plans.ts` — add `FEATURE_COPY: Record<EntitlementKey, { label; description; docHref? }>`.
- [x] 1.2 `HOMEPAGE_FEATURES: EntitlementKey[]` — the curated, ordered subset for the homepage.
- [x] 1.3 Selectors: `tiersForDisplay()`, `lowestTierWith(key)`, `entitlementKeys()`.
- [x] 1.4 Build-time completeness check: catalog entitlement keys === `FEATURE_COPY` keys;
      throw on mismatch. Run it from a module that the build imports (or a test).

## 2. Pricing page

- [x] 2.1 Delete `PLANS` and `ROWS` literals from `pricing/page.tsx`.
- [x] 2.2 Render tier cards from `tiersForDisplay()`; feature rows from
      `entitlementKeys()` + `FEATURE_COPY` + per-tier values.
- [x] 2.3 Placeholder-price caveat driven by `price.placeholder`.
- [x] 2.4 Keep Founding Setup + continuity sections as authored prose.

## 3. Features page

- [x] 3.1 `web/src/app/(marketing)/features/page.tsx` — OSS-engine list (authored prose,
      links to docs) + hosted table generated from the catalog.
- [x] 3.2 Metadata, `DocHeader`, consistent with the other marketing pages.

## 4. Homepage + nav

- [x] 4.1 Homepage feature section sources items from `HOMEPAGE_FEATURES` + `FEATURE_COPY`.
- [x] 4.2 `site-header.tsx` nav + `sitemap.ts` — add `/features`.

## 5. Validation

- [x] 5.1 `openspec validate marketing-site-dynamic-content --strict` passes.
- [x] 5.2 `web/`: `npm run build` + `npx eslint` green.
- [x] 5.3 Test: pricing comparison-table row count === catalog entitlement count;
      completeness check fails on an injected missing/orphan key.
      Row count is structural — the table maps over `entitlementKeys()`. The
      missing/orphan check is `assertFeatureCopyComplete()` in `plans.ts`, run at module
      load, so `next build` fails on drift. `web/` has no JS unit runner (noted in the
      plan-catalog-and-entitlements change, 5.2); adding one stays out of scope.

## Decisions to lock (from proposal Open Questions)

- [x] D1 OSS-engine list stays authored prose. (proposed)
- [x] D2 `/features` is a separate page from `/pricing`. (proposed)
- [x] D3 Placeholder prices show the number with the caveat. (proposed)
