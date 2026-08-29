## Context

See proposal.md for why. Relevant existing facts:
- `globals.css` already has the full shadcn CSS-custom-property architecture (light block + dark
  block under `.dark`, `@theme inline` mapping) — every token this change touches already exists
  and is already threaded through every component via Tailwind's `bg-primary`/`ring`/etc. utility
  classes. This is a values-only change to that file, not a restructuring of it.
- `Badge` (`web/src/components/ui/badge.tsx`) already has `default`/`secondary`/`destructive`/
  `outline`/`ghost`/`link` variants via `cva`, and is already used for plan tier, token status, and
  case-report PASS/FAIL on the dashboard (`web/src/app/dashboard/page.tsx:93,274,315`) — but not for
  flag-proof leg outcomes (`page.tsx:354-367`), which render as plain `TableCell` text with no
  color.
- `next-themes` is a declared dependency (`package.json`) but no `ThemeProvider` is mounted anywhere
  in `layout.tsx`, and no toggle control exists — dark mode is fully inert today despite being
  "installed."
- `lucide-react` is a declared dependency, unused in `web/src/app/dashboard/page.tsx` or
  `web/src/app/page.tsx` today.
- The dashboard's eight cards (`page.tsx`) render in a single fixed order, one per backend entity,
  regardless of project state — a brand-new project with nothing configured yet shows the exact same
  layout as one with months of history.

## Goals / Non-Goals

**Goals:**
- A real, deliberately-chosen accent color that survives sitting next to PASS/FAIL status colors
  without competing, applied through the existing token architecture with no new CSS structure.
- Consistent use of `Badge` for every status field on the dashboard, not a subset.
- A working light/dark toggle, since the infrastructure for it is already paid for.
- A dashboard layout that reflects what a customer is doing (setting up vs. running vs. checking
  results) rather than which backend entity a card happens to map to.
- A landing page that shows the product does something, not just states that it exists.

**Non-Goals:**
- No component-library or CSS-framework change (see proposal.md).
- No new interaction complexity beyond what's needed for the zone grouping — no drag-and-drop
  reordering, no per-user customizable layout, no new client-side state management beyond a single
  collapse/expand toggle.
- Not redesigning the visual builder (`web/src/app/journeys/[journeyId]/journey-builder.tsx`) in
  this pass — it's functionally dense (dynamic step/parameter/header/capture rows) and deserves its
  own design pass once the base token/component work here lands and can be reused there.
- Not solving typography (serif heading / sans body mix) definitively here — see Open Questions.

## Decisions

**Accent hue: indigo (`oklch(~0.55 0.18 275)`), not teal or amber.** Chosen for hue distance from
the two colors this product already uses meaningfully: PASS (green, ~145°) and FAIL/`destructive`
(red-orange, ~27°). Indigo at 275° is roughly maximally distant from both on the wheel, so a
customer's eye never confuses "this is the brand accent" with "this is a pass/fail signal" — a real
risk with amber (60°), which sits close enough to destructive's 27° to read as a warning state in
some contexts. Teal (195°) was the next candidate; indigo wins on legibility as "developer/technical
tool" (same lane as Linear, Vercel, Raycast) without being a generic default.

**Apply the hue conservatively, not uniformly.** `--primary` and `--ring` get the real hue at
meaningful chroma (interactive/brand elements: primary buttons, focus rings, the Paid-tier badge,
active nav state). `--secondary` and `--accent` stay near-neutral — a light, low-chroma tint of the
same hue rather than the current flat gray, so they still visually recede behind primary actions
instead of competing with them. `--chart-1..5` become a sequential lightness ramp of the same hue
(useful once any usage/trend visualization exists) instead of five shades of gray.

**Badge consistency is a mechanical audit, not a new component.** Every status-shaped value
currently rendered as plain text (starting with the flag-proof leg outcomes at `page.tsx:354-367`)
gets wrapped in the existing `Badge` with the appropriate variant (`default` for pass/configured,
`destructive` for fail, `secondary` for neutral/inactive states like revoked tokens or not-yet-run).
No new variants needed — the six that exist already cover every status this product has.

**Dark mode: mount `next-themes`' `ThemeProvider` at the root, toggle in the header next to the
Clerk `UserButton`.** `next-themes` already handles the flicker-free system-preference default and
localStorage persistence shadcn's own theme block assumes (the `.dark` class selector in
`globals.css` is already written for exactly this). Toggle placement in the header (not buried in a
settings page) because it's a page-wide, low-frequency preference — the same place GitHub, Linear,
and Vercel put theirs.

**Dashboard zones: group with section headers, not tabs — and "Set up" collapses once something is
configured.** Full tabs would hide Run/Results behind a click for a returning customer, which is
the opposite of what a runs-list-as-home-screen pattern (GitHub Actions, Linear) wants. Instead:
three visually distinct, always-scrollable sections (a section label + tighter card grouping, not a
new interactive primitive), with "Set up" specifically collapsed to a single summary line
("3 of 3 configured — Azure DevOps, LaunchDarkly, GitHub") once at least one of
Connection/Adapter-credentials/Project-secrets has something configured, expanded by default only
for a brand-new project with nothing set up yet. This is the one piece of new client-side state in
this change (a single expanded/collapsed boolean, no persistence needed — recomputed from server
data on load).

**Zone order: Set up → Run → Results.** Not Results-first: a brand-new project has no run history to
show, so leading with an empty Results section is a worse first impression than leading with the
(collapsed-when-done) setup section that explains what to do next. A returning customer's setup
section is collapsed to one line anyway, so the ordering costs them nothing.

**Landing page: a real product screenshot (the post-refresh dashboard, run through this same design
system) plus 3–4 one-line feature callouts, not a rewritten value proposition.** The existing copy
("Self-serve release-proof testing...") is accurate and stays; the gap is that nothing visually
demonstrates it. Lowest-risk way to close that gap without inventing new marketing content.

## Risks / Trade-offs

- **A single global hue change touches every page at once** — no incremental rollout path within
  this codebase's current structure (no feature-flagging of CSS). → Accepted: this is a values-only
  edit to already-shared tokens: verifying it in Cypress screenshots (the suite's existing
  `cy.screenshot(...)` convention) before merge is the practical safety net, not a staged rollout.
- **The "Set up" collapse-when-configured logic is new conditional UI state, however small.** →
  Mitigated by keeping it a single derived boolean with no persistence — worst case it's wrong for
  one page load and self-corrects on the next data fetch, never a stuck/broken state.
- **Choosing indigo is a real aesthetic judgment call, not a measured outcome.** → Accepted and
  named explicitly rather than presented as objective; revisit if the design partner (or a future
  customer) pushes back once it's live — the token architecture makes changing the single hue value
  cheap later.
- **`next-themes` is unusable in this project's exact Next.js/React combination** — its
  unconditionally-rendered inline `<script>` throws "Encountered a script tag while rendering React
  component" on any client-side re-render, blanking the page. Not next-themes-specific: a plain JSX
  `<script>` and Next's own official `next/script` (`beforeInteractive`) hit the identical failure.
  → Mitigated by hand-rolling `theme-provider.tsx` (context + `useSyncExternalStore`, no `<script>`
  anywhere) instead of depending on the package; `next-themes` uninstalled. Accepted consequence: no
  anti-flash-of-wrong-theme script, so a cold load can briefly show the light theme first — see
  tasks.md 3.1 for the full writeup.

## Open Questions

Resolved during implementation: `globals.css`'s `--font-sans: var(--font-sans)` was a circular
self-reference that never actually resolved to the real Geist font — the *entire page*, not just
headings, was silently falling back to the browser's serif default (confirmed via computed style).
Fixing that one line (`--font-sans: var(--font-geist-sans)`) answers the serif/sans question by
construction, since `--font-heading` was already aliased to `--font-sans`: both body and headings
now render in the real, modern Geist sans-serif face. See tasks.md 8.1.
