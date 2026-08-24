## Context

`@clerk/testing`'s Cypress integration (confirmed against Clerk's own docs, not assumed) provides `clerkSetup()` (wired into `cypress.config.ts`'s `setupNodeEvents`, fetches a short-lived Testing Token that bypasses Clerk's bot detection for automated runs) and `addClerkCommands()` (wired into `cypress/support/e2e.ts`, adds `cy.clerkSignIn`/`cy.clerkSignOut`). `cy.clerkSignIn` needs a real, already-existing account to sign in as — it doesn't create one. Of its three strategies (`password`, `phone_code`, `email_code`), only `password` avoids depending on real email/SMS delivery reaching a test inbox during a CI run, which is why proposal.md scopes to it specifically — this also means **password must be enabled as a sign-in method on the Clerk instance itself**, a manual dashboard step separate from whatever real customers see (Google + email, per the instance as configured earlier this session).

This project has no CI pipeline yet at all (README: "no ... GitHub Action" is listed only for CLI packaging, but no workflow file exists anywhere in the repo) — see proposal.md's Non-Goals for why wiring Cypress into CI is explicitly a separate future step, not bundled here.

## Goals / Non-Goals

**Goals:**
- Automate the exact walkthrough already done by hand this session (real Clerk sign-in → dashboard → create project → issue token → sign out), runnable locally with one command.
- Keep the test user's creation scripted and idempotent — never a manually-clicked-together dashboard account with no record of how it was made.

**Non-Goals:**
- CI wiring (proposal.md).
- GitHub Connections coverage (still blocked on a registered OAuth App).
- Testing anything the hosted API's own `dotnet test` suite already covers directly (org-scoping, token revocation logic, etc.) — Cypress is for what only a real browser + real Clerk session can prove: that the actual rendered pages, real Clerk widget, and real network calls between the two services actually work together.

## Decisions

**Test-user provisioning lives in a Cypress Node task, not a standalone script.** Register `task('ensureE2ETestUser', ...)` inside `cypress.config.ts`'s `setupNodeEvents`, implemented with `@clerk/backend`'s `createClerkClient({ secretKey: process.env.CLERK_SECRET_KEY })`. The task looks up `clerkClient.users.getUserList({ emailAddress: [testEmail] })` first and only calls `createUser` if none exists — idempotent by construction, no separate "did I already run this" bookkeeping needed. Called once via a root-level `before()` hook in the e2e spec.
- *Alternative considered*: a standalone `scripts/ensure-e2e-user.ts` run as an npm `pretest` step. Rejected — keeping it inside Cypress's own lifecycle means one command (`npm run e2e`) does everything; a separate script is one more thing to remember to run first.

**Test data isolation**: the hosted API, when started for e2e, deliberately gets **no** `Database__SqlitePath` or connection string — falling back to its existing in-memory database, which is already a real, tested code path (`Program.cs`'s existing three-way fallback). Every e2e run starts from a genuinely clean slate for free, with zero cleanup logic needed, at the cost of the same test user's Clerk *identity* persisting across runs (fine — `ProvisioningService.GetOrCreateUserAsync` already handles "same identity, different runs" correctly, and organization/project data resets each run regardless since it lives in the now-ephemeral DB).
- *Alternative considered*: a dedicated SQLite file, deleted before each run. Rejected — more moving parts (file deletion step, path config) for no benefit over a fallback that already exists and is already tested.

**Booting both services**: `start-server-and-test`, the standard companion package for exactly this "wait for N servers to be healthy, run the test command, tear down" shape — one entry for the hosted API (`dotnet run --project ../hosted/ReleaseTwin.Hosted.Api --urls http://localhost:5199`, health-checked against `http://localhost:5199/api/dashboard` — a 401 still counts as "the server responded," which is all `start-server-and-test` needs) and one for `next dev` on `http://localhost:3000`.
- *Alternative considered*: a shell script hand-rolling health-check polling. Rejected — `start-server-and-test` is the ecosystem-standard tool for this, not worth reinventing.

**Env var bridging**: `@clerk/testing`'s documented convention is `CLERK_PUBLISHABLE_KEY`/`CLERK_SECRET_KEY` (no `NEXT_PUBLIC_` prefix), while `web/.env.local` already has `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` for the Next.js app itself and `CLERK_SECRET_KEY` (already unprefixed, no bridging needed there). Cypress's own config needs `CLERK_PUBLISHABLE_KEY` set too — either duplicate the value under both names in `.env.local`, or read `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` and pass it through explicitly in `cypress.config.ts`'s `clerkSetup({ publishableKey: process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY, ... })` (verify at implementation time which `clerkSetup()` accepts — its options are typed and will make this concrete quickly rather than needing to guess here).

## Risks / Trade-offs

- [The dedicated test user requires `password` enabled as a sign-in method on the real Clerk instance — a manual dashboard toggle, separate from what real customers see] → Mitigation: same category of one-time manual setup as everything else this session (Clerk app itself, GitHub OAuth App) — not new complexity, just one more line item.
- [`cy.clerkSignIn` explicitly doesn't support MFA — if the Clerk instance ever requires MFA for all users, the scripted test user would need an explicit exemption or MFA would need staying opt-in] → Mitigation: not a concern today (no MFA configured); worth remembering if enterprise-auth work ever happens (already named as "come after paid demand is demonstrated" in `docs/installation-model.md`).
- [Running against the in-memory DB fallback means this never exercises the SQLite/Postgres code paths] → Mitigation: not this change's job — `dotnet test`'s existing suite already runs against real SQLite/in-memory `HostedDbContext` configurations; Cypress's job is the browser/Clerk/network layer, not the persistence layer.
