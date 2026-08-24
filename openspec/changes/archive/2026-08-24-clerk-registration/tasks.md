## 1. Entity and data model

- [x] 1.1 Rename `AppUser.GitHubId` → `ClerkUserId` and `AppUser.GitHubLogin` → `DisplayName` (src/hosted `Data/Entities/AppUser.cs`), updating the doc comment to no longer say "authenticated via GitHub OAuth."
- [x] 1.2 Move the unique index in `HostedDbContext` from `GitHubId` to `ClerkUserId`.

## 2. Provisioning

- [x] 2.1 Rename `ProvisioningService.GetOrCreateUserAsync`'s parameters from `gitHubId`/`gitHubLogin` to `clerkUserId`/`displayName`; keep behavior identical (first call provisions a personal organization, no human approval).
- [x] 2.2 Update `/dev/seed` (Program.cs, Development-only) to seed a synthetic Clerk-shaped identity instead of a `dev-{login}` GitHub-shaped one.

## 3. Auth wiring

- [x] 3.1 Replace `.AddGitHub(...)` with `.AddOAuth("Clerk", options => {...})` in Program.cs, sourcing Client ID/Secret/endpoint URLs from configuration (never hardcoded).
- [x] 3.2 Map Clerk's userinfo response to `ClaimTypes.NameIdentifier` (Clerk user ID), a display-name claim, and email — verifying the actual claim names against a real Clerk application's userinfo response.
- [x] 3.3 Update `OnCreatingTicket`-equivalent event handler to call `ProvisioningService.GetOrCreateUserAsync` with the Clerk-shaped identity and add the same `org_id`/`user_id` claims as today.
- [x] 3.4 Update `Login.cshtml.cs` to challenge the `"Clerk"` scheme instead of GitHub's.
- [x] 3.5 Update `Index.cshtml`'s copy to not name GitHub specifically (e.g. "Sign in to get started" rather than "Sign in with GitHub").

## 4. Sign-out behavior (design.md risk)

- [ ] 4.1 Once a real Clerk application is available for manual testing, verify whether `/Logout` silently re-authenticates on the next `/Login` (Clerk's own session cookie surviving a local-only sign-out). If it does, extend `Logout.cshtml.cs` to also revoke the session at Clerk before the local `SignOutAsync` call.

## 5. Tests

- [x] 5.1 Update `hosted/ReleaseTwin.Hosted.Api.Tests/*` references to GitHub-specific naming/comments (`DashboardModelTests.cs`, `DashboardHttpTests.cs`) to match the new Clerk-shaped identity and scheme name.
- [x] 5.2 Confirm `ProvisioningServiceTests.cs` still exercises "first login auto-creates an organization" and "second login with the same identity returns the existing user" with the renamed parameters.
- [x] 5.3 `dotnet test ReleaseTwin.Hosted.slnx` clean, no live Clerk application required (matches today's "no live GitHub OAuth App is needed to run them").

## 6. Docs

- [x] 6.1 Update README.md's "Self-serve signup" section (env var names, "GitHub OAuth App" → "Clerk application") and its "What's not built yet" line naming a registered GitHub OAuth App.
- [x] 6.2 Update `docs/customer-pilot-guide.md` and `docs/installation-model.md` wherever they mention GitHub OAuth specifically as the sign-in mechanism.

## 7. Manual verification (requires a real Clerk application — outside this repo)

- [ ] 7.1 Register a Clerk application, enable at least one sign-in method, set the callback URL, supply Client ID/Secret via environment variables, and confirm the full landing-page → sign-in → dashboard flow works end to end locally.
