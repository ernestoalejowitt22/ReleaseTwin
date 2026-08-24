## Context

`Program.cs` wires GitHub via `AspNet.Security.OAuth.GitHub`'s `.AddGitHub(...)`, which is itself a thin, provider-specific wrapper around ASP.NET Core's generic `OAuthHandler<T>`/`AddOAuth`. Clerk can be consumed the same way — as a plain OAuth provider via `.AddOAuth("Clerk", ...)` — because Clerk exposes an OAuth-compatible authorize/token/userinfo endpoint set for external apps to authenticate against it (its "OAuth Applications" feature), not just its own first-party embedded-widget mode. See proposal.md for why GitHub was never a deliberate fit.

`AppUser` (`hosted/ReleaseTwin.Hosted.Api/Data/Entities/AppUser.cs`) is the only entity keyed by GitHub identity today (`GitHubId` unique-indexed in `HostedDbContext`). `ProvisioningService.GetOrCreateUserAsync` is the single provisioning entrypoint, called from `Program.cs`'s `OnCreatingTicket` — first login auto-creates a personal organization, no separate "create org" step (account-provisioning: "Signup requires no human approval").

## Goals / Non-Goals

**Goals:**
- Swap the concrete OAuth provider from GitHub to Clerk, same architectural shape (`AddOAuth`, cookie session, `OnCreatingTicket` → provisioning).
- Make `AppUser` provider-neutral in its own field names, since the app never needs to know which sign-in method a Clerk user actually used.
- Keep the "no human approval, immediately usable" self-serve behavior byte-for-byte the same.

**Non-Goals:**
- Configuring more than one sign-in method inside Clerk for this change. Clerk's dashboard can be given email/password, magic link, Google, GitHub, etc. independently of this code change — which methods are actually enabled is an operator decision made in Clerk's dashboard, not something this change needs to decide or implement per-method.
- The Connections feature (`project-connections`, a separate change) — no dependency in either direction.
- Migrating any existing GitHub-identified account. None exist outside this repo (README: "not yet offered to anyone"), so there is nothing to migrate.

## Decisions

**Provider wiring**: replace `.AddGitHub(options => {...})` with `.AddOAuth("Clerk", options => {...})`, configured with Clerk's Client ID, Client Secret, and instance-specific authorize/token/userinfo endpoint URLs — all env-supplied via configuration (`Clerk:ClientId`, `Clerk:ClientSecret`, `Clerk:Domain` or similar), matching the existing "no hardcoded credential literal" discipline already enforced (and tested — `AdapterSourceContainsNoCredentialLiteral`-style check) elsewhere in this codebase. The exact userinfo claim shape (which field carries a display name — `name`, `username`, or similar) needs verifying against Clerk's actual OAuth userinfo response at implementation time; this is a small implementation detail, not a decision that changes the approach, so it's not blocking this design.
- *Alternative considered*: embed Clerk's hosted `<SignIn/>`/`<SignUp/>` JS components directly in `Login.cshtml`. Rejected — bigger integration surface (a JS SDK in a server-rendered Razor Pages app) for no benefit over the OAuth-redirect pattern that already works today and needs zero new client-side code.

**`AppUser` shape**: `GitHubId` → `ClerkUserId` (unique-indexed, same as today), `GitHubLogin` → `DisplayName` (falls back to whatever Clerk's userinfo gives when no separate display name exists — mirrors today's `login ?? gitHubId` fallback in `Program.cs`). `Email` stays as-is (already nullable, already provider-agnostic).

**Provisioning**: `ProvisioningService.GetOrCreateUserAsync(clerkUserId, displayName, email, ...)` — same method, renamed parameters, identical behavior (first call creates a personal organization; subsequent calls with the same `ClerkUserId` return the existing user).

**Dev-seed endpoint**: `/dev/seed` (Development-only, Program.cs) updates its synthetic identity from `dev-{login}` (a fake GitHub ID) to a synthetic Clerk-shaped ID, so local walkthroughs without a registered Clerk application keep working exactly as they do today without a registered GitHub App.

## Risks / Trade-offs

- [Clerk's own session cookie (set on Clerk's domain during the OAuth redirect) could cause `/Login` to silently re-authenticate a user who only signed out locally, since today's `Logout.cshtml.cs` only calls `HttpContext.SignOutAsync` on the local cookie scheme] → Mitigation: verify this behavior once a real Clerk application exists; if it reproduces, `Logout` needs to also revoke the session at Clerk (a documented Clerk API call) before the local sign-out, not just the local cookie. Flagging now so it isn't discovered as a surprise during manual testing.
- [Clerk is a new external vendor dependency the product now depends on for every login] → Mitigation: this is the explicit trade this change is making — trading a GitHub-specific accidental dependency for a deliberate managed-auth-provider dependency, per the original design doc's own risk mitigation (D7: "use managed infra... a managed auth/OAuth provider" instead of self-hosting security-critical pieces).
- [No official Clerk-published ASP.NET Core SDK exists; the community `Clerk.Net` package isn't needed for this change since plain `AddOAuth` suffices, but if a future change needs Clerk's management API (e.g. to enumerate a customer's linked identities), it would depend on an unofficial, community-maintained package] → Mitigation: not a concern for this change (no such dependency is introduced here); worth remembering as a constraint if a future change reaches for it.
