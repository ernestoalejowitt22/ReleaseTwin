## Context

See `proposal.md` - Why. Investigated directly (not assumed) before writing this:

- **Sign-up already works today, via Clerk's hosted Account Portal.** `/sign-in`'s `<SignIn/>` widget's "Sign up" link points to `https://classic-marlin-8065.accounts.dev/sign-up?...` — Clerk's own hosted domain, not a local `/sign-up` route (`web/src/app/layout.tsx`'s `<ClerkProvider>` sets no `signUpUrl` override, so Clerk falls back to its default). No new frontend route needs to be built; the registration spec needs to follow this cross-origin link and drive Clerk's own hosted form.
- **`@clerk/testing`'s Cypress package has no `clerkSignUp` helper** — only `clerkSignIn`, `clerkSignOut`, `clerkLoaded` (`node_modules/@clerk/testing/dist/types/cypress/custom-commands.d.ts`). Sign-up must be driven with raw `cy.get`/`cy.type`/`cy.click` against Clerk's real widget DOM, same category of thing `cypress.config.ts` already anticipated (`chromeWebSecurity: false`, with its own comment about the `accounts.dev` cross-domain bounce).
- **`ensureE2ETestUser` (existing)** pre-creates a Clerk user via the backend admin API — this is *not* reused by the new registration spec, which needs the opposite: a disposable address that has never been created, so the sign-up form's own account-creation path actually runs.
- **The CLI is a local `dotnet run` process** (`src/ReleaseTwin.Cli`), reading `RELEASETWIN_API_TOKEN`/`RELEASETWIN_API_URL` from the environment and uploading to whatever `RELEASETWIN_API_URL` points at (`ReleaseTwin.Cli/Program.cs`). Cypress specs run in-browser and can't shell out directly — invoking a local process needs a `cy.task`, the same mechanism `ensureE2ETestUser` already uses.

## Goals / Non-Goals

**Goals:**
- `registration.cy.ts` proves a real, never-before-seen `+clerk_test@` address can sign up through Clerk's actual hosted UI and lands on a working, provisioned dashboard.
- `product-usage-loop.cy.ts` proves the full loop — issue a token, actually run the CLI against it, see the result reflected back in the dashboard — not just that each piece works in isolation.
- Both specs are independently runnable and don't depend on each other's state.

**Non-Goals:**
- A fresh Clerk identity generated per run for the registration spec — one disposable `+clerk_test@` address, reused across runs, is sufficient per explicit decision. (This means the spec must tolerate "this address may already exist" on repeat local runs — see Decisions.)
- Building a local `/sign-up` route — not needed; Clerk's hosted Account Portal already serves this.
- Testing the Docker distribution path from `cli-packaging` — no version has actually been tagged/published yet (that task was deliberately deferred), so `product-usage-loop.cy.ts` runs the CLI from source (`dotnet run`), matching what a customer without a published image would also do today.
- Any change to product/API behavior — this is test-only, `skip_specs: true`.

## Decisions

**Registration spec: use a fixed disposable address, and handle "already exists" as a valid outcome, not a failure.** Since the same address is reused run over run (per explicit decision — not a fresh identity each time), a second run against an already-registered address would hit Clerk's "account already exists" state on the hosted sign-up form instead of completing a fresh signup. The spec should detect this and treat it as acceptable (the first-ever run is what actually proves the sign-up path; subsequent runs mainly confirm the app doesn't break when Clerk redirects an existing user back). Use Clerk's documented `+clerk_test@` OTP convention (verification code `424242`) — confirm this empirically against the real hosted form during implementation rather than assuming it's unchanged.

**Registration spec drives the real cross-origin hosted form, not a mocked one.** Following the existing precedent in `cypress.config.ts` (`chromeWebSecurity: false` specifically for this bounce) — `setupClerkTestingToken()`, click through to `accounts.dev`, fill the real form fields, submit, land back on `/dashboard`. This is slower and more fragile than driving a local route would be, but it's what "real registration" means for an app that intentionally uses Clerk's hosted portal rather than embedding its own sign-up UI.

**`product-usage-loop.cy.ts` uses a new `runCli` Cypress task**, added to `cypress.config.ts` alongside `ensureE2ETestUser`, shelling out via Node's `child_process` to `dotnet run --project <path-to-src>/ReleaseTwin.Cli -- examples/cases` with `RELEASETWIN_API_TOKEN` (the token just issued through the UI) and `RELEASETWIN_API_URL` (pointing at the same hosted API instance the e2e run already boots, `http://localhost:5199`) set in the child process's environment. Returns exit code + stdout so the test can assert the CLI actually ran successfully, not just that the dashboard changed for unrelated reasons.

**The loop test uses the bundled zero-credential example (`examples/cases/example-http.yaml`)** — same one already referenced in the token-onboarding instructions shown in the UI, so the test is exercising exactly the command a real customer would copy-paste, not a bespoke test fixture.

**Verification after the CLI run: reload `/dashboard`, assert the run history table shows the new case and the usage counter incremented by 1** — both already-existing, already-tested UI elements (`dashboard-walkthrough.cy.ts`'s "Run history" table, `usage-metering`'s usage card); this test's job is confirming they reflect a *real* CLI-driven upload, not re-testing their own rendering.

**Timeouts: the `dotnet run` task needs a generous Cypress task timeout** (a first invocation includes an implicit build) — set explicitly on the `cy.task('runCli', ...)` call rather than relying on Cypress's default command timeout, which is tuned for UI interactions, not a `dotnet build`.

## Risks / Trade-offs

- [Driving Clerk's real hosted sign-up form is inherently more brittle than testing a local route — a Clerk UI change could break this spec independent of anything in this repo] → Accepted; this is the actual, real registration surface, so testing against it (even if brittle) is more honest than mocking it.
- [Reusing one disposable address means the "fresh signup" path is only truly exercised the very first time this spec ever runs; every subsequent run exercises the "already exists" branch instead] → Accepted per explicit decision; documented in the spec's own comments so this isn't mistaken for full coverage on every run.
- [Shelling out to `dotnet run` from a Cypress task adds real wall-clock time (a .NET build) to the e2e suite] → Accepted; this is the whole point of `product-usage-loop.cy.ts` — proving the real, unmocked path, not keeping the suite fast.
- [The CLI's bundled HTTP example case calls a live public API (`jsonplaceholder.typicode.com`) — this test now has an external network dependency beyond Clerk] → Accepted; already true of every local/CI run of the CLI's own example today (see `README.md`), not a new dependency introduced by this test.

## Open Questions

(none — the sign-up-widget question was resolved above; everything else needed to write tasks.md was decided in the preceding conversation)
