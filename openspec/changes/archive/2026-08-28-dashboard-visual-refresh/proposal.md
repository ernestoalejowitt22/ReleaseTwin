## Why

The product currently ships shadcn's unmodified default "neutral" theme: every color token in
`globals.css` has zero chroma (`oklch(x 0 0)`) except `--destructive`, which is the library's own
built-in default. No brand hue was ever chosen. The component library that would carry a real
identity — `Badge` variants, `next-themes` dark mode, `lucide-react` icons — is installed and wired
in places but mostly unused (no dark-mode toggle exists anywhere; several status fields render as
plain table text instead of the `Badge` component already used elsewhere on the same page). The
dashboard itself is a flat, undifferentiated stack of eight bordered cards ordered by which backend
entity they came from (Project, Journey, AdapterCredential, ProjectSecret, Connection, ApiToken,
CaseReport, FlagProofResult) rather than by what a customer is actually trying to do on the page.
The landing page is a single centered text block with no visual demonstration of the product. None
of this blocks functionality — every real workflow validated so far (signup, GitHub connection,
adapter/project secrets, journeys, CLI uploads) works end to end — but the product currently reads
as an unstyled MVP rather than something a design partner would trust.

## What Changes

- Choose one real brand hue and thread it through the existing CSS custom-property architecture
  (`--primary`, `--ring`, `--sidebar-primary`, `--chart-1..5` as a sequential ramp) — the token
  plumbing to do this already exists and needs no restructuring, only real values.
- Apply the component primitives that are installed but idle: consistent `Badge` usage for every
  status field (run outcomes, flag-proof legs, configured/not-configured states — not just some of
  them), icons on section headers/nav via the already-installed `lucide-react`, and a working
  light/dark toggle via the already-installed `next-themes` (currently dead weight in
  `package.json`).
- Regroup the dashboard's eight cards into three intent-based zones — Set up (Connection, Adapter
  credentials, Project secrets), Run (Journeys, API tokens), Results (Run history, Flag-proof
  results) — instead of the current flat, entity-ordered stack, so the page reflects what a
  customer is doing rather than what backend model each card maps to.
- Give the landing page an actual visual case for the product instead of a single centered text
  block with one button.
- **Explicit Non-Goal**: not a framework or component-library swap. shadcn/Tailwind/radix stay
  exactly as they are — this is exercising and configuring what's already installed, not replacing
  it.
- **Explicit Non-Goal**: not a rewrite of any page's actual data or interaction logic. Every
  existing server action, form, and data fetch keeps its current behavior; only presentation
  changes.

## Capabilities

Pure visual/presentational change — no new or modified externally observable behavior, no new API
contract, no new data. `skip_specs: true` set in this change's `.openspec.yaml`.

## Impact

- `web/src/app/globals.css`: real chroma values for the primary/ring/sidebar/chart token set, in
  both the light and dark blocks already present.
- `web/src/app/layout.tsx`: mount `next-themes`' `ThemeProvider`; add a toggle control (exact
  placement — header vs. user menu — is a design decision, see design.md).
- `web/src/app/dashboard/page.tsx` and its section components: reorganize into the three zones;
  apply `Badge` consistently to every status field that currently renders as plain text (the
  flag-proof leg outcomes and any others found during implementation).
- `web/src/app/page.tsx` (landing page): real visual content beyond the current single centered
  block — exact content (screenshot, feature highlights, etc.) is a design decision, see design.md.
- No change to `hosted/ReleaseTwin.Hosted.Api`, the CLI, or any adapter — this is `web/`-only.
