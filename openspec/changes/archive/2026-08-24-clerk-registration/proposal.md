## Why

The hosted platform's web-session login is hardcoded to GitHub OAuth (`Program.cs`'s `.AddGitHub(...)`, `AppUser.GitHubId`/`GitHubLogin`, `ProvisioningService.GetOrCreateUserAsync(gitHubId, ...)`), even though `openspec/specs/account-provisioning/spec.md` and `openspec/specs/dashboard/spec.md` were already written provider-neutral ("via email or an OAuth provider," "via OAuth or a magic-link login"). GitHub was never a deliberate product fit — the archived `hosted-self-serve-platform` design doc (D3) named "GitHub OAuth or email magic link" as the two options and only GitHub got built, because a ready-made ASP.NET Core package existed for it (matches D2's "single-person effort" reasoning), not because ReleaseTwin's actual customer has any reason to have a GitHub account. The product's own adapters target Azure DevOps and arbitrary REST APIs — requiring a GitHub account just to *try* the dashboard is an accidental filter on who can sign up, not a real product requirement.

This change replaces the GitHub-specific login with Clerk, a managed auth provider, wired the same way GitHub is today — Clerk supports being consumed via ASP.NET Core's standard `AddOAuth` middleware, the same mechanism `AddGitHub` already uses, so this is a like-for-like swap in shape, not a rewrite. Clerk becomes the one integration point for however many sign-in methods get configured later (email/password, magic link, Google, GitHub-as-one-option) — all Clerk-dashboard configuration, not new code per method.

## What Changes

- `AppUser` drops `GitHubId`/`GitHubLogin` in favor of a provider-neutral external identity (`ClerkUserId` + a display name), since the app no longer needs to know or care which sign-in method a customer actually used inside Clerk. **BREAKING**: existing GitHub-identified accounts (none exist outside this repo yet, per README's "not yet offered to anyone") have no migration path — this is fine only because no App has been registered and no real customer has ever signed up.
- `Program.cs`'s `.AddGitHub(...)` OAuth handler is replaced with `.AddOAuth("Clerk", ...)`, configured against Clerk's Client ID/Secret/domain (env-supplied, never hardcoded — same discipline as every existing adapter's credential handling).
- `ProvisioningService.GetOrCreateUserAsync` takes a Clerk user ID instead of a GitHub ID; behavior (first login auto-provisions an organization, no human approval) is unchanged.
- `Login.cshtml.cs` challenges the `"Clerk"` scheme instead of GitHub's.
- `Index.cshtml`'s landing-page copy no longer names GitHub specifically.
- The dev-only `/dev/seed` endpoint (Program.cs) is updated to seed a Clerk-shaped user instead of a `dev-{login}` GitHub-shaped one.

**Explicitly out of scope**: the Connections feature (linking a project to an external GitHub/Bitbucket/Azure DevOps repo/org) — that's a separate, independent change (`project-connections`) with no dependency on this one. No change to billing (still doesn't exist), no change to the ingest API's token-based auth (untouched — Clerk only affects the web-session auth domain, not the CLI's bearer-token upload path).

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `account-provisioning`: signup mechanism becomes explicitly provider-neutral by construction (Clerk-backed), not just provider-neutral in spec wording while GitHub-specific in implementation. `dashboard`'s web-session requirement was already written provider-neutral ("via OAuth or a magic-link login") and needs no wording change — only its implementation changes.

## Impact

- `hosted/ReleaseTwin.Hosted.Api/Data/Entities/AppUser.cs`: field rename/generalization (`GitHubId`/`GitHubLogin` → `ClerkUserId`/`DisplayName`).
- `hosted/ReleaseTwin.Hosted.Api/Data/HostedDbContext.cs`: unique index moves from `GitHubId` to the new identity field.
- `hosted/ReleaseTwin.Hosted.Api/Program.cs`: auth scheme registration, `OnCreatingTicket`-equivalent claim mapping, `/dev/seed` dev endpoint.
- `hosted/ReleaseTwin.Hosted.Api/Services/ProvisioningService.cs`: parameter rename, same behavior.
- `hosted/ReleaseTwin.Hosted.Api/Pages/Login.cshtml.cs`, `Pages/Index.cshtml`: scheme name and copy.
- `hosted/ReleaseTwin.Hosted.Api.Tests/*`: tests referencing GitHub-specific naming/comments updated to match.
- New external dependency: a Clerk application (free tier), configured with at least one sign-in method — a one-time, manual, account-specific setup step outside this repo, same shape as the GitHub OAuth App registration it replaces (README's "What's not built yet" list already carries an equivalent line for GitHub; this change swaps what that line names).
- No change to `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, any adapter, or the CLI.
