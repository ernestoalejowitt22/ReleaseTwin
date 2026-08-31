## 1. Shared copy + selectors

- [ ] 1.1 `web/src/lib/plans.ts` — add `FEATURE_COPY: Record<EntitlementKey, { label; description; docHref? }>`.
- [ ] 1.2 `HOMEPAGE_FEATURES: EntitlementKey[]` — the curated, ordered subset for the homepage.
- [ ] 1.3 Selectors: `tiersForDisplay()`, `lowestTierWith(key)`, `entitlementKeys()`.
- [ ] 1.4 Build-time completeness check: catalog entitlement keys === `FEATURE_COPY` keys;
      throw on mismatch. Run it from a module that the build imports (or a test).

## 2. Pricing page

- [ ] 2.1 Delete `PLANS` and `ROWS` literals from `pricing/page.tsx`.
- [ ] 2.2 Render tier cards from `tiersForDisplay()`; feature rows from
      `entitlementKeys()` + `FEATURE_COPY` + per-tier values.
- [ ] 2.3 Placeholder-price caveat driven by `price.placeholder`.
- [ ] 2.4 Keep Founding Setup + continuity sections as authored prose.

## 3. Features page

- [ ] 3.1 `web/src/app/(marketing)/features/page.tsx` — OSS-engine list (authored prose,
      links to docs) + hosted table generated from the catalog.
- [ ] 3.2 Metadata, `DocHeader`, consistent with the other marketing pages.

## 4. Homepage + nav

- [ ] 4.1 Homepage feature section sources items from `HOMEPAGE_FEATURES` + `FEATURE_COPY`.
- [ ] 4.2 `site-header.tsx` nav + `sitemap.ts` — add `/features`.

## 5. Validation

- [ ] 5.1 `openspec validate marketing-site-dynamic-content --strict` passes.
- [ ] 5.2 `web/`: `npm run build` + `npx eslint` green.
- [ ] 5.3 Test: pricing comparison-table row count === catalog entitlement count;
      completeness check fails on an injected missing/orphan key.

## Decisions to lock (from proposal Open Questions)

- [ ] D1 OSS-engine list stays authored prose. (proposed)
- [ ] D2 `/features` is a separate page from `/pricing`. (proposed)
- [ ] D3 Placeholder prices show the number with the caveat. (proposed)
