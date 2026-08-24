## 1. Frontend scaffold

- [x] 1.1 Pick a shadcn/ui-based starting template against the real page inventory (landing, sign-in, dashboard with projects/tokens/run-history/flag-proof/connections) — not a batteries-included admin panel with unused demo pages.
- [x] 1.2 Scaffold `web/` (Next.js, TypeScript, Tailwind) at the repo root, sibling to `hosted/`.
- [x] 1.3 Install and configure `@clerk/nextjs`; wire `<ClerkProvider>` and middleware-based route protection for the dashboard routes.
- [x] 1.4 Configure the API base URL via an env var matching the CLI's existing `RELEASETWIN_API_URL` naming convention.

## 2. Hosted API: auth scheme swap

- [x] 2.1 Remove the cookie + `AddOAuth("Clerk", ...)` scheme, `Login.cshtml(.cs)`, `Logout.cshtml(.cs)` from `Program.cs`/`Pages/`.
- [x] 2.2 Add `Microsoft.AspNetCore.Authentication.JwtBearer`, registered as a distinctly-named scheme (e.g. `"ClerkJwt"`) validating against Clerk's JWKS (using the existing `Clerk:Domain` config value).
- [x] 2.3 Move provisioning from `OnCreatingTicket` to `JwtBearerOptions.Events.OnTokenValidated`, calling `ProvisioningService.GetOrCreateUserAsync` and attaching `org_id`/`user_id` claims exactly as today.
- [x] 2.4 Confirm every dashboard-equivalent endpoint explicitly restricts to the `"ClerkJwt"` scheme (`AddAuthenticationSchemes`), matching the pattern `IngestEndpoints.cs` already uses for `ApiTokenDefaults.Scheme`.
- [x] 2.5 Add a test: a valid API token presented to a dashboard-equivalent endpoint is rejected; a valid Clerk JWT presented to an ingest endpoint is rejected (the new spec requirement).

## 3. Hosted API: pages → JSON endpoints

- [x] 3.1 Convert `Index.cshtml` (landing page content) into whatever Next.js needs — likely nothing server-side at all, since it's static marketing content.
- [x] 3.2 Convert `Dashboard.cshtml.cs`'s `OnGetAsync`/`OnPostCreateProjectAsync`/`OnPostIssueTokenAsync`/`OnPostRevokeTokenAsync`/`OnPostDisconnectAsync` into JSON API endpoints (minimal APIs or controllers), preserving every org-scoping check unchanged.
- [x] 3.3 Convert `Connections/Start.cshtml.cs` and `Connections/Callback.cshtml.cs` into redirect/JSON endpoints: `Start` stays a redirect (browser navigates through the GitHub OAuth dance directly), `Callback` exchanges the code server-side exactly as today but returns JSON (repo list) instead of rendering a Razor page; add a `Confirm` JSON endpoint for the picker's final choice.
- [x] 3.4 Delete the now-unused `.cshtml` files once their logic has moved.

## 4. React pages

- [x] 4.1 Landing page (React, using the chosen template's marketing/hero patterns).
- [x] 4.2 Sign-in via `@clerk/nextjs`'s `<SignIn/>` component.
- [x] 4.3 Dashboard: projects list/create, API tokens list/issue/revoke (with the "shown once" token warning preserved), run history table, flag-proof results table (kept visually distinct from ordinary case results, per the existing spec requirement).
- [x] 4.4 Connections: "Connect GitHub" link, repo picker (calling the new callback-result JSON endpoint), connected-repo display, disconnect action.

## 5. Tests

- [x] 5.1 Hosted API: update/replace `DashboardModelTests.cs`, `DashboardHttpTests.cs`, `ConnectionFlowTests.cs` for the new JSON-endpoint shape (same org-scoping scenarios, new transport).
- [x] 5.2 Hosted API: `dotnet test ReleaseTwin.Hosted.slnx` clean, no live Clerk application required (matches the existing testing pattern).
- [x] 5.3 Frontend: whatever test setup the chosen template ships with (component/unit level) — no new Playwright/Cypress dependency introduced speculatively; revisit only if a real need for browser-level testing emerges (see the earlier explore-mode discussion).

## 6. Docs

- [x] 6.1 README.md: replace the single `dotnet run` hosted-platform walkthrough with the two-process setup (API + Next.js dev server).
- [x] 6.2 `docs/installation-model.md`: update the "Hosted control plane" description and the "Default vs. opt-in functionality" table to reflect the new frontend stack.

## 7. Manual verification (requires the real Clerk application already registered)

- [ ] 7.1 Full walkthrough: landing page → sign in via Clerk → dashboard → create project → issue token → connect GitHub → run the CLI against the issued token → confirm results appear on the dashboard.
