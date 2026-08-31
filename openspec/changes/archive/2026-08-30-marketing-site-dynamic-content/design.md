## Context

See proposal.md — Why. The pricing page hardcodes a `PLANS` array and a `ROWS`
comparison table; there is no `/features` page. The plan catalog
(`hosted/plans.json` + `web/src/lib/plans.ts`, landed by
`plan-catalog-and-entitlements`) is the enforced source of truth, but nothing on
the marketing surface reads it.

Constraints that shape the approach:

- `web/` has **no JS unit-test runner** (documented in
  `plan-catalog-and-entitlements` tasks 5.2). Build-time enforcement has to be a
  module-load side effect that `next build` exercises, not a test.
- `web/src/lib/plans.ts` already imports `../../../hosted/plans.json` — a path
  outside `web/`. Before this change no built page imported `plans.ts`, so
  Turbopack never had to resolve it.
- Marketing pages are React Server Components; `plans.ts` runs at module load on
  the server. Throwing there fails the build, which is the desired behaviour.

## Goals / Non-Goals

**Goals:**

- The pricing page, a new `/features` page, and the homepage feature section all
  render tier/entitlement content from one source (`plans.ts`), so they cannot
  drift from the catalog or from each other.
- A new catalog entitlement cannot ship without display copy — enforced at build.

**Non-Goals:**

- No backend change. `GET /plans` and the C# entitlement enforcement are
  untouched.
- No CMS / data file for the open-source-engine capability list — it stays
  authored prose (proposal D1).
- Not fixing the pre-existing doubled `— ReleaseTwin — ReleaseTwin` page titles
  (every marketing subpage has this; out of scope).

## Decisions

### D1 — `FEATURE_COPY` is a typed `Record<EntitlementKey, …>` in `plans.ts`

Label + one-line description + optional `docHref` per entitlement key. Lives next
to the catalog loader so the completeness check is a local concern.

- **Why not a JSON/MDX file:** the copy is tied 1:1 to the TypeScript
  `EntitlementKey` union; a `Record<EntitlementKey, …>` gets compile-time
  completeness for missing keys for free, and co-locates with `ENTITLEMENT_KEYS`.
- **Alternative rejected:** a `features.json` alongside `plans.json` — adds a
  second file to keep in order with no type safety and no editing-experience win
  for a 10-entry map.

### D2 — Build-time completeness = `assertFeatureCopyComplete()` at module load

Runs the same way `validateCatalog()` already does. Catches the orphan case (copy
key not in `ENTITLEMENT_KEYS`) and the JSON-drift case (catalog/union gains a key
with no copy) that the type system alone misses.

- **Alternative rejected:** a unit test — `web/` has no runner; adding one for a
  single assertion is disproportionate. A module-load throw is strictly stronger
  (it also gates `next dev`).

### D3 — Tier cards keep an authored `TIER_META` map keyed by tier id

The catalog supplies name, price, `price.placeholder`, `unit`, `support` and
every entitlement value. `TIER_META` holds only *framing* that isn't in the
catalog: the one-line blurb, the CTA label/href/variant, and the "billed
annually" footnote. Keyed by the closed `"free" | "team" | "enterprise"` union,
so a card can't reference a tier the catalog doesn't have.

- This satisfies the spec's "no page-local list of tiers, prices, or per-tier
  feature values" — `TIER_META` has none of those. Per-tier feature values moved
  entirely into the catalog-driven comparison table.
- **Alternative rejected:** deriving blurbs/CTAs from the catalog too — the
  catalog has no field for marketing copy and shouldn't; adding one couples the
  enforced contract to the website.

### D4 — `/features` is its own route, not a section of `/pricing` (proposal D2)

Pricing answers "what does it cost", features answers "what can it do". The
homepage and nav link both. Keeps each page short. The features page renders
outside the docs layout (its own `<main>` container, like `/pricing`) but reuses
`DocHeader` for a consistent masthead.

### D5 — Turbopack `root` moves from `web/` to the repo root

`next.config.ts` pinned `turbopack.root` to `__dirname` (`web/`), which stops
Turbopack resolving `../../../hosted/plans.json`. The catalog genuinely lives
outside `web/` (it's shared with the hosted API), so widening `root` to the repo
root is correct — not a workaround.

- **Alternative rejected:** a prebuild step copying `plans.json` into `web/` — a
  generated file to gitignore and keep fresh, when the import already works once
  the resolver is allowed to see it.

### D6 — Placeholder prices show the number with a caveat (proposal D3)

`formatPrice()` renders `~$49` when `price.placeholder`, `$49` otherwise; a
per-card caveat line renders for any tier with `price.placeholder`. A soft anchor
beats "contact us" for an evaluator, and the caveat matches the site's tone.

## Risks / Trade-offs

- **A malformed `plans.json` now fails `next build`, not just the API.** →
  Intended. The catalog is already shape-checked on the C# side; this makes the
  web build fail loudly instead of rendering wrong prices. `validateCatalog()`
  error messages name the offending tier/key.
- **Widening Turbopack `root` could pick up a parent lockfile / change workspace
  inference.** → The repo has a single lockfile at the web level; build verified
  green (20/20 pages) after the change. If a future monorepo restructure adds a
  root lockfile, revisit.
- **`FEATURE_COPY` completeness is only checked at build, not in an isolated
  test.** → Acceptable given no runner; the check gates every `next build` and
  `next dev`, and CI runs `next build`.

## Migration Plan

Pure frontend, no data migration. Deploy is the standard `web/` build on push to
`main`. Rollback = revert the commit; no persistent state involved.
