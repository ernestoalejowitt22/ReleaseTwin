## Context

Today: `Program.cs` registers `CookieAuthenticationDefaults.AuthenticationScheme` (web session) and `ApiTokenDefaults.Scheme` (CLI uploads) as two structurally distinct mechanisms — a cookie versus a bearer token — so confusing one for the other isn't just guarded against, it's not even expressible. `IngestEndpoints.cs` already explicitly restricts itself via `.RequireAuthorization(policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes(ApiTokenDefaults.Scheme))` — not "any authenticated user," a named scheme. This change removes the cookie scheme and adds a second Bearer-shaped one (Clerk JWT), which is what makes the new "don't accept one for the other" requirement necessary — before, the two credential *shapes* made confusion structurally impossible; after, only the code's explicit scheme restriction does.

`CurrentOrganizationAccessor` currently reads an `org_id` claim placed on the principal at sign-in time by `clerk-registration`'s `OnCreatingTicket` handler, which called `ProvisioningService.GetOrCreateUserAsync` once, at login. A Clerk JWT bearer token carries only what Clerk itself knows (`sub` = Clerk user id) — it was never told about ReleaseTwin's own `Organization`/`AppUser` rows, so there is no `org_id` claim to read off it, ever. See proposal.md for why this changes (BFF + JWKS-validated tokens vs. a same-origin cookie).

## Goals / Non-Goals

**Goals:**
- Replace the UI layer end to end (landing, sign-in, dashboard, connections picker) with React/Next.js/Tailwind, in one change (per the explore-mode discussion, not phased).
- Make the two Bearer-shaped credential domains (API token, web-session JWT) provably non-interchangeable, not just accidentally distinct.
- Preserve every existing behavioral spec (`dashboard`, `account-provisioning`, `project-connections`) unchanged — this is an implementation swap for those, not a behavior change (hence no deltas for them).

**Non-Goals:**
- Choosing the exact shadcn/ui-based starting template right now — a real, current list exists (Apex Dashboard, shadcn-admin, Tailwindadmin, Dashboard Shell) with different trade-offs (batteries-included vs. minimal-and-extend); pick at implementation time against the actual page inventory needed (landing, sign-in, a dashboard with projects/tokens/run-history/flag-proof/connections — closer to a small SaaS app shell than a full admin panel with charts/CRM/etc.).
- Reintroducing Bitbucket/Azure DevOps connections, billing, or CLI packaging — untouched by this change.
- Server-side rendering strategy details (which pages are Server Components vs. Client Components) beyond what's needed for the BFF pattern to hold — an implementation-time call per page, not a decision to make in the abstract here.

## Decisions

**BFF pattern, no CORS.** Next.js server components/route handlers are the only caller of the .NET API; the browser never calls it directly. This is why no CORS configuration appears anywhere in this design — there's no cross-origin browser request to allow. Also keeps the Clerk session token off the browser's JS-accessible surface entirely (Next.js middleware reads it server-side).

**JWT bearer auth via Clerk's JWKS**, added as a distinctly-named scheme (e.g. `"ClerkJwt"`), replacing the cookie + `AddOAuth("Clerk", ...)` scheme from `clerk-registration` outright — not layered alongside it. `Microsoft.AspNetCore.Authentication.JwtBearer`'s standard `Authority`/JWKS-discovery configuration points at Clerk's Frontend API domain (the same `Clerk:Domain` config value `clerk-registration` already introduced).
- *Alternative considered*: keep the cookie scheme and have Next.js somehow forward it. Rejected — fights the BFF pattern (a cookie is tied to a specific origin/domain in ways a bearer token isn't meant to be), and throws away the exact thing Clerk's JWT-plus-JWKS model exists to provide (verifiability by any backend, in any language, without a shared session store).

**Provisioning moves from `OnCreatingTicket` to `JwtBearerOptions.Events.OnTokenValidated`** — the direct analog: both fire once a credential is confirmed valid, before the request proceeds. `OnTokenValidated` calls `ProvisioningService.GetOrCreateUserAsync(clerkUserId, ...)` (already idempotent — first call creates, later calls return the existing row) and adds the resulting `org_id`/`user_id` as claims on the validated principal, so `CurrentOrganizationAccessor` keeps reading claims exactly as it does today — only where those claims get attached changes, not how they're consumed downstream.
- *Alternative considered*: resolve the organization via a DB lookup inside `CurrentOrganizationAccessor` itself on every access, dropping the claims-based read entirely. Rejected — `OnTokenValidated` already runs once per request (not once per property access), and keeping `CurrentOrganizationAccessor`'s existing claims-reading shape means no downstream consumer (`DashboardModel`-successor endpoints, tests) needs to change how it asks "who is this."

**Scheme-restriction is the actual enforcement mechanism for "not interchangeable."** No new code should be needed beyond registering both schemes distinctly and giving each protected endpoint group an explicit `AddAuthenticationSchemes(...)` restriction — the pattern `IngestEndpoints.cs` already uses for `ApiTokenDefaults.Scheme`. The new spec requirement mainly guards against a future dashboard-equivalent endpoint being added without that explicit restriction (defaulting to "any authenticated scheme," which would silently accept an API token).

**`project-connections` OAuth flow stays server-side in .NET**, converted from Razor Pages (`Start.cshtml`/`Callback.cshtml`) to plain redirect/JSON endpoints. `ConnectionStateService`, the token-exchange-then-discard logic, and the "never persisted" guarantee all move unchanged — only `Callback.cshtml`'s picker *rendering* moves to a React page that calls a new JSON endpoint (e.g. `GET /api/connections/callback-result?state=...`) for the repo list instead of receiving server-rendered HTML.

**Repo layout**: `web/` at the repo root, sibling to `hosted/`, `src/`, `tests/`, `examples/` — matches the existing flat top-level layout (`hosted/` is already a separately-deployable solution next to the main one).

**Local dev**: two processes — `dotnet run --project ReleaseTwin.Hosted.Api` and the Next.js dev server — communicating via an env var named the same way the CLI's `RELEASETWIN_API_URL` already is, for consistency across every place this codebase points a client at the hosted API.

## Risks / Trade-offs

- [Two services to run and deploy instead of one — real new operational surface for a single-person project, the exact cost D2 originally weighed and declined] → Mitigation: this is the explicit trade the user is choosing to make now; not a cost to hide, worth remembering if "who's going to run two things in production" becomes a real question later.
- [JWT-based session revocation has different timing characteristics than a server-owned cookie: a Clerk JWT is typically valid until it expires (short-lived, refreshed by Clerk's SDK), so revoking a user at Clerk doesn't instantly invalidate an already-issued token still inside its validity window the way deleting a server-side cookie/session would] → Mitigation: verify Clerk's actual token lifetime defaults at implementation time and confirm they're short enough that this isn't a meaningful window for this product's risk profile (no sensitive data crosses the ingest API regardless — D6's existing invariant already limits blast radius here).
- [Introducing Next.js means introducing Node.js as a runtime dependency for the first time anywhere in this repo, plus npm's dependency-supply-chain surface] → Mitigation: accepted cost of the decision already made (React/Tailwind); not new information, just naming it plainly.
- [A shadcn/ui-based template not chosen carefully could bring far more (charts, CRM/e-commerce demo pages, features never needed) than this product's actual page inventory — visible bloat and maintenance surface] → Mitigation: Non-Goals above defers the exact pick to implementation time, against the real page list, not a template's marketing page.
