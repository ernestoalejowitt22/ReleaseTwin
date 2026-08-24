## Why

The hosted platform's UI is server-rendered Razor Pages — a deliberate choice at the time (`hosted-self-serve-platform` design.md D2: "a modern JS framework for the dashboard — rejected for now given this is a single-person effort... without a demonstrated need for it"). That need now exists: the user wants a React + Tailwind UI built on a proven shadcn/ui-based template rather than hand-styled Razor pages.

This isn't just a reskin. It reverses D2 deliberately (first npm/JS toolchain in the repo, a second deployable service) and it changes how `clerk-registration` was built: that change wired Clerk via ASP.NET Core's generic `AddOAuth` specifically because Razor Pages had no better integration point. Clerk's actual first-party product is `@clerk/nextjs` — prebuilt `<SignIn/>`/`<SignUp/>`/`<UserButton/>` components, middleware-based route protection, JWT session tokens designed to be verified by any backend via JWKS. Moving to Next.js doesn't just restyle the Clerk integration, it replaces a workaround with the thing Clerk actually built for this.

## What Changes

- New Next.js + React + Tailwind + shadcn/ui-based application (`web/`, sibling to `hosted/`) owns all UI: landing page, sign-in (via `@clerk/nextjs`), and the dashboard (projects, tokens, run history, flag-proof results, connections).
- `ReleaseTwin.Hosted.Api` becomes an API-only backend: all `Pages/*.cshtml` (`Index`, `Login`, `Logout`, `Dashboard`, and the `Connections/*` picker UI) are retired in favor of JSON endpoints. `Login.cshtml.cs`'s OAuth challenge and the cookie/`AddOAuth("Clerk", ...)` scheme from `clerk-registration` are removed entirely, not extended.
- The hosted API authenticates web-originated requests via **Clerk-issued JWT bearer tokens**, validated against Clerk's JWKS endpoint (`Microsoft.AspNetCore.Authentication.JwtBearer`) — not a same-origin cookie. Next.js acts as a backend-for-frontend (BFF): the browser never calls the .NET API directly, only Next.js (server components/route handlers), which attaches a Clerk session token when it does. No CORS configuration is needed as a result.
- `CurrentOrganizationAccessor`'s mechanism changes: a Clerk JWT only carries Clerk's own `sub` (user id) — it has no idea about ReleaseTwin's `Organization`/`AppUser` rows. Resolving the signed-in organization now requires a DB lookup by `ClerkUserId` on each authenticated request (via `JwtBearerOptions.Events.OnTokenValidated`, the direct analog of `OAuthEvents.OnCreatingTicket`), not a claim already sitting on the principal.
- The `project-connections` GitHub OAuth flow (state minting, token exchange, "never persisted") stays server-side in the .NET API — converted from Razor Pages to plain JSON/redirect endpoints, not reimplemented in Next.js. The trust-sensitive part is already solved there; only the picker's presentation moves to React.
- **New risk this specifically introduces**: the web-session credential (a Clerk JWT) and the CLI's API token are now both presented as `Authorization: Bearer <value>` — before this change they were structurally distinct (cookie vs. bearer), impossible to confuse. This change adds an explicit requirement that neither is accepted in place of the other.

**Explicitly out of scope**: any change to `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, any adapter, the CLI, or the ingest API's payload contract. No design decision here revisits billing, Bitbucket/Azure DevOps connections, or CLI packaging.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `ingest-api`: adds the requirement that a web-session credential (Clerk JWT) does not satisfy API-token authentication, now that both are Bearer-shaped.

## Impact

- New top-level `web/` directory: a Next.js app with its own `package.json`, TypeScript, Tailwind, and a shadcn/ui-based starting template (specific template chosen at implementation time — see design.md).
- `hosted/ReleaseTwin.Hosted.Api/Pages/*`: `Index.cshtml`, `Login.cshtml(.cs)`, `Logout.cshtml(.cs)`, `Dashboard.cshtml(.cs)`, `Connections/Start.cshtml(.cs)`, `Connections/Callback.cshtml(.cs)` all removed or converted to non-page (API/redirect) endpoints.
- `hosted/ReleaseTwin.Hosted.Api/Program.cs`: removes the cookie + `AddOAuth("Clerk", ...)` scheme, adds JWT bearer auth against Clerk's JWKS; the existing `ApiToken` bearer scheme is unchanged but now must be explicitly, provably distinct from the new scheme.
- `hosted/ReleaseTwin.Hosted.Api/Services/CurrentOrganizationAccessor.cs`, `ProvisioningService.cs`: mechanism changes (claim-read → DB lookup keyed by `ClerkUserId`; provisioning trigger moves from `OnCreatingTicket` to `OnTokenValidated`), behavior (self-serve, no human approval) unchanged.
- Local dev and deployment become two processes/services instead of one: `dotnet run` (API) + Next.js dev server, communicating over HTTP via an env var mirroring the CLI's existing `RELEASETWIN_API_URL` convention.
- README.md, `docs/installation-model.md` updated to reflect the two-service local setup and the new frontend stack.
