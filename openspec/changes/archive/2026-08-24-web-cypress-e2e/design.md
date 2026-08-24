## Context

`@clerk/testing`'s Cypress integration (confirmed against Clerk's own docs, not assumed) provides `clerkSetup()` (wired into `cypress.config.ts`'s `setupNodeEvents`, fetches a short-lived Testing Token that bypasses Clerk's bot detection for automated runs) and `addClerkCommands()` (wired into `cypress/support/e2e.ts`, adds `cy.clerkSignIn`/`cy.clerkSignOut`). `cy.clerkSignIn` needs a real, already-existing account to sign in as — it doesn't create one.

**Revised during implementation**: this design originally planned `password` strategy specifically to avoid real email/SMS delivery in CI. Running it for real against the actual Clerk instance proved that wrong — Clerk's **Device Trust** feature (rolled out November 2025, discovered only by hitting it, not documented anywhere I found in advance) auto-requires a second factor for *any* password sign-in from a device Clerk hasn't seen before, with **no supported bypass** — per-user, per-instance, or via Testing Tokens (verified against Clerk's own Device Trust docs). Every automated test run is "a new device" to Clerk, so this isn't an edge case, it's every single run. The fix: `email_code` strategy instead, paired with Clerk's documented `+clerk_test@` test-address convention (fixed, known verification code, no real delivery — the property `password` was originally chosen for, achieved a different way). Device Trust only applies to password sign-ins, so `email_code` sidesteps it entirely. This instance still *requires* a password at account-creation time regardless of sign-in strategy (`skipPasswordChecks: true` with a random throwaway value satisfies that without ever using it to sign in).

This also means password auth needed to be confirmed enabled as a sign-in method on the Clerk instance — verified directly (typed the test user's email into the real embedded `/sign-in` page, confirmed it prompted for a password) rather than assumed.

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

**Env var bridging**: resolved via `clerkSetup()`'s typed `options.publishableKey` — pass `process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` directly, no duplicated env var name needed. It parses the Frontend API domain from the publishable key itself.

**`chromeWebSecurity: false`** (discovered during implementation, not planned): Clerk *development* instances (`*.accounts.dev` shared infra, not a custom domain) bounce the browser through their own `accounts.dev` origin to establish cross-domain session cookies — happens even with our own embedded `<SignIn/>` component, not just Account Portal redirects. Cypress's default same-origin enforcement turns that bounce into a hard failure (`cy.origin()` would technically fix it, but awkwardly, since the flow needs to return to `localhost` afterward). Disabling it is the documented pragmatic escape hatch for exactly this class of issue. A production Clerk instance on a real custom domain wouldn't hit this at all.

**`email_code` avoids Device Trust, but not by accident** — see Context above for why `password` was abandoned mid-implementation.

## Risks / Trade-offs

- [`cy.clerkSignIn` explicitly doesn't support MFA — if the Clerk instance ever requires MFA for all users, `email_code` sign-in itself might stop being sufficient] → Mitigation: not a concern today (no MFA configured); worth remembering if enterprise-auth work ever happens (already named as "come after paid demand is demonstrated" in `docs/installation-model.md`).
- [Running against the in-memory DB fallback means this never exercises the SQLite/Postgres code paths] → Mitigation: not this change's job — `dotnet test`'s existing suite already runs against real SQLite/in-memory `HostedDbContext` configurations; Cypress's job is the browser/Clerk/network layer, not the persistence layer.
- [`chromeWebSecurity: false` is a real, if narrow, security-relevant setting change — acceptable in a test-only Cypress config that never touches production code, but worth remembering it exists if this config is ever copied elsewhere] → Mitigation: scoped entirely to `web/cypress.config.ts`, never shipped, never affects the real app's own security posture.

## Bug found and fixed by this change's own verification (task 4.2)

Running the real e2e test against the real Clerk instance surfaced a genuine bug in `hosted-react-frontend`'s JWT bearer setup that no prior verification caught (unit tests used a placeholder Clerk domain; the manual walkthrough never got past the sign-in screen): ASP.NET Core's `JwtBearerHandler` remaps short inbound claim names (`sub`, `email`, `name`, ...) to legacy XML-namespaced `ClaimTypes` URIs by default. `Program.cs`'s `OnTokenValidated` read `context.Principal.FindFirst("sub")` literally, which never matched — throwing `"Clerk JWT is missing a 'sub' claim"` on every real, valid token, a 500 on every dashboard load. Fixed with `options.MapInboundClaims = false`, keeping claim types exactly as they appear in the JWT. This also incidentally confirms something design.md previously flagged as unverified: Clerk's OAuth Authorization Server metadata document (`/.well-known/oauth-authorization-server`) *is* compatible with ASP.NET Core's OIDC-shaped configuration manager — token validation reached claims-reading, meaning JWKS discovery and signature verification both worked correctly.
