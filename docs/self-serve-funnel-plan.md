# Self-serve funnel — engineering plan

What has to be built for the go-to-market model in `docs/go-to-market.md` (open core + free
self-serve funnel + SaaS tiers). Organized as workstreams, each of which should become one or
more OpenSpec changes when picked up. Ordered roughly by dependency, not priority.

The guiding cut: **launch the funnel (rungs 0–2 free, rung 3 = "email us") before building
Stripe.** Manual invoicing validates that anyone hits the walls and asks, before you spend weeks
on billing nobody exercises.

---

## Current state (what already exists)

| Capability | Status |
|---|---|
| CLI + Core + adapters (HTTP, Azure DevOps, LaunchDarkly, UI) | ✓ built, ~340 tests, CI green |
| Docker image, tag-triggered release workflow | ✓ `ghcr.io/ernestoalejowitt22/releasetwin/cli` |
| Data-driven HTTP adapter (test any REST API from case data) | ✓ |
| Hosted platform: Clerk signup, projects, tokens, ingest, dashboard, evidence viewer | ✓ deployed on AWS, prod Clerk instance wired |
| `plan-tier-gating` — Free/Paid tier, 1-project Free cap, self-serve no-payment upgrade | ✓ built (placeholder for real billing) |
| `usage-metering` — uploaded report count per org per month | ✓ built (observability, no enforcement) |
| Evidence capture Paid-tier gated, CLI-side redaction, S3 blob store, scheduled purge | ✓ |
| Landing page (`web/src/app/page.tsx`) — hero, live dashboard preview, feature grid | ✓ exists, needs funnel copy |
| Auto-deploy from `main` (Lambda API + Vercel) | ✓ but targets a `releasetwin-dev-` prefixed stack |

---

## Workstream A — Open-source licensing

Small, do first — it unblocks the whole open-core narrative and costs little.

- **Add `LICENSE`** — Apache-2.0 for `src/**`, `tests/**`, `examples/**`. There is currently **no
  license file at all**, which legally means all-rights-reserved.
- **Decide the commercial boundary** for `hosted/**` and `web/**`: BSL 1.1 (converts to Apache
  after N years, no-compete in the interim) in the same repo, or move them to a private repo.
  Recommendation: BSL in-repo, simplest to operate solo.
- `CONTRIBUTING.md`, `SECURITY.md`, GitHub issue/PR templates.
- **Scrub git history for secrets** before making the repo public — verify the
  `hosted/terraform-bootstrap` MFA-comment note and confirm no AWS keys / Clerk secrets / PATs
  were ever committed. (`git log -p -S` sweeps, or `trufflehog`/`gitleaks`.)
- Flip the repo public (or split), add topics, a real description, a short GIF in the README.

**No dependencies. ~1 change.**

---

## Workstream B — Rung 1 friction: get started in 10 minutes without cloning

Today "write your own case" means clone the repo for `examples/` and hand-build a `cases/` +
`fixtures/` layout.

- **`releasetwin init` / `releasetwin new <case>`** — scaffold command that writes a `cases/` +
  `fixtures/` skeleton and a starter `http.request` + `http.assertJsonPath` case with comments.
  New capability in `cli-runner` / `case-loading`.
- **Bundle `examples/` into the Docker image** (or have `init` emit them) so
  `docker run … --help`-level discovery works without the repo.
- **Config-driven adapter selection** — a `releasetwin.yaml` that names which adapters to load,
  replacing the hardcoded "HTTP always, Azure DevOps if 5 env vars" logic in
  `src/ReleaseTwin.Cli/CliRunner.cs`. Listed as deferred in the README; needed for rungs 1–2 to
  feel like a real product rather than a demo harness. New capability, touches `cli-runner`.
- **Quickstart doc / docs section**: "Test your first API in 10 minutes" — standalone, not
  buried in the README.
- **Terminal recording asset** (asciinema `.cast` + a rendered SVG/GIF) of a `FLAGPROOF` run for
  the landing page. `demo/naha-releasetwin-flow.mp4` is a starting point but too long.

**Depends on: nothing hard. ~2–3 changes.**

---

## Workstream C — Distribution surfaces

- **`dotnet tool` / NuGet package** for the CLI — `dotnet tool install -g ReleaseTwin.Cli`.
  Extends the existing `release.yml` workflow. Deferred in the README.
- **GitHub Action wrapper** — `releasetwin/action@v1` (thin wrapper over the Docker image),
  published to the GitHub Marketplace. This is the natural rung-2 "wire into CI" path for the
  GitHub-Actions majority.
- **Azure DevOps Marketplace listing** — the adapter already exists; a task/extension wrapper +
  listing is mostly packaging + marketing copy.

**Depends on: B (config-driven adapters makes the Action ergonomic). ~2–3 changes.**

---

## Workstream D — Make the hosted platform actually public

The platform is deployed but nothing links to signup and the auto-deploy stack is dev-prefixed.

- **Stand up a real prod stack** — a `releasetwin-prod-` (or unprefixed) terraform
  workspace/prefix, confirm the wired Clerk instance is a **production** instance (not
  `*.clerk.accounts.dev`), custom domains + TLS for the API (currently a raw Lambda Function
  URL) and the web app. Update `hosted-deploy-state` memory once done.
- **Landing-page funnel copy** (`web/src/app/page.tsx`): the no-signup `docker run` CTA, the
  trust section, the design-partner note. Per `go-to-market.md` sketch.
- **Pricing page** — a new route rendering the Free / Team / Enterprise tier table; Enterprise =
  a contact form (form → email, no CRM yet).
- **Open registration** — link "Create a free account" from the landing page; make sure the
  sign-up flow works for a cold stranger (no allowlist, no invite gate).
- **Terms of Service + Privacy Policy pages** — required before public signup, and non-optional
  given evidence handling. Plain, honest, short. (Legal review eventually; a first draft
  unblocks launch.)
- **Funnel analytics** — you can already measure signups (Clerk) and activation (first uploaded
  report, from ingest / `usage-metering`). Add a minimal internal view or scheduled digest:
  signups, activated orgs, orgs hitting the Free project cap. Privacy-respecting page analytics
  (Plausible-style, self-host or none) for the marketing pages.

**Depends on: A (public repo is part of the story), B (something worth signing up for). ~3–4
changes + ops work.**

---

## Workstream E — Conversion walls (product-led, cheap)

Make the Free-tier limits visible with a clear next step — this is what turns rung 2 into rung 3.

- **Decide Free-tier limits** — start with what exists (1 project) plus: evidence viewer off,
  30-day retention. Maybe a monthly uploaded-report soft cap (metered already, not enforced).
- **In-dashboard upgrade prompts** at each wall: second-project attempt (already rejected —
  make the rejection a friendly upgrade CTA), evidence viewer ("See request/response detail and
  screenshots — available on Team"), retention slider capped at 30 with an upgrade note.
- **Wire `plan-tier-gating`'s Paid flag to the real entitlements** — evidence viewer, retention
  max, project cap all read one plan state.
- **Rung-3 CTA initially = "email us to upgrade"** (mailto or a form). No Stripe yet. This is
  deliberate: it validates that anyone actually hits the walls before billing is built.

**Depends on: D (public platform with real users). ~1–2 changes.**

---

## Workstream F — Billing (only after E shows demand)

- **Stripe** — Checkout for Team self-serve, Customer Portal for management, webhook →
  plan-tier state in DynamoDB (replaces the no-payment placeholder in `plan-tier-gating`).
- **Plan model** — Free / Team with enforced limits; Enterprise stays manual (invoice + custom
  terms).
- **Enforcement** already has its seams from E; F just flips the upgrade path from "email us" to
  "checkout".
- **Dunning / failed-payment handling**, receipts, tax (Stripe Tax). Keep minimal.

**Depends on: E, and real signal that people hit the walls. ~2–3 changes.**

---

## Minimum to launch the funnel

Rungs 0–2 free and working, rung 3 = "email us", no Stripe:

**A** (license + public repo) + **B** (scaffold + config adapters + quickstart + demo asset) +
**D minus analytics depth** (prod stack, landing copy, pricing page, open signup, ToS/Privacy).

Defer **C** (NuGet/Action — add once signup works), **E** polish, and **F** entirely until the
funnel shows people arriving and hitting walls.

---

## Open questions

- Prod stack: reuse the auto-deploy pipeline with a prod prefix, or a separate manual prod
  pipeline? (`hosted-deploy-state` memory has the current dev-prefix detail.)
- BSL vs private repo for `hosted/` + `web/`.
- Does `visual-flake-classification` (already proposed) become the *wedge* the landing page
  leads with, instead of "release proof" broadly? Decide after the first validation calls — it
  has a crisper problem statement and an existing category.
- Free-tier monthly report cap: enforce, soft-cap, or none at launch?
