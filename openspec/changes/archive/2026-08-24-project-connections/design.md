## Context

`clerk-registration` wired Clerk via ASP.NET Core's `AddOAuth`/cookie-remote-authentication pipeline: `Challenge()` → redirect → callback → `OnCreatingTicket` → `SignInAsync` with a `ClaimsPrincipal`, establishing a persistent session. That pipeline exists to *authenticate the current user*. This change needs something structurally different: a one-shot "get a token, call an API once, throw the token away, remember only what was picked" flow, scoped to a specific project, that must never result in a session, cookie, or stored credential — see proposal.md for why (display metadata only, no credential custody).

## Goals / Non-Goals

**Goals:**
- Prove the Connection concept (entity, UI, disconnect) with GitHub, the simplest of the three named providers.
- Make "the token is never persisted" a structural property of the implementation, not just a documented intention — provable by reading the code, not just by testing for absence.

**Non-Goals:**
- Bitbucket, Azure DevOps (future changes, once this pattern is validated).
- Any use of the connected repo beyond a display label (proposal.md).
- Refreshing or re-validating a connection's repo still existing/being accessible over time — a connection, once made, is just a stored label until disconnected. Revisit only if staleness becomes an actual problem.

## Decisions

**Don't reuse `AddOAuth`/remote-authentication middleware for the connection flow.** Hand-roll a minimal authorization-code exchange instead: a "Connect GitHub" link starts the flow with a signed/opaque `state` value encoding the target project ID and a CSRF nonce (not a bare project ID — an attacker who could forge the callback shouldn't be able to attach an arbitrary repo to an arbitrary project); GitHub redirects back to a callback action that exchanges the code for a token via a direct `HttpClient` POST to GitHub's token endpoint (not middleware), immediately calls GitHub's repo-list API with that token held only in a local variable, and renders the picker in that same response. The customer's subsequent "confirm" POST carries only the chosen repo's identifier forward — never the token. This makes "never persisted" checkable by inspection: the token variable's scope never leaves the one method that fetches the repo list, and no code path serializes it.
- *Alternative considered*: register a second `AddOAuth` scheme (e.g. `"GitHubConnect"`) purely for this flow, short-circuiting before `SignInAsync` in `OnCreatingTicket`. Rejected — the remote-authentication handler pipeline is built around eventually calling `SignInAsync`/establishing a principal; using it for a "do a side effect, don't sign in" flow means fighting the abstraction (suppressing its default behavior) rather than using it as designed, for a flow that's genuinely simpler than what that pipeline solves.

**`Connection` entity**: one connection per project (not a list) — `ProjectId` (unique, one connection per project), `Provider` (string, `"github"` for now — not an enum, so adding Bitbucket/Azure DevOps later doesn't require a migration touching this column's type), `ExternalRepo` (e.g. `"acme-corp/checkout-service"`), `ConnectedAt`. No token, no refresh token, no scope list — nothing GitHub-credential-shaped is ever a column on this table, by construction.

**GitHub OAuth App scope**: request the narrowest scope that lists repositories (GitHub's `repo` scope is broader than needed if only public repos matter — verify at implementation time whether a read-only/public-only scope suffices for the target customer profile, or whether private-repo visibility is actually required for this to be useful; this is a real product question — a customer whose repo is private needs a broader scope than one who's fine labeling a public repo — worth deciding based on what "acme-corp/checkout-service" actually looks like for a real early customer, not guessed here).

**State/CSRF**: the `state` parameter must be unguessable and tied to the current web session (e.g. HMAC-signed with a server secret, embedding project ID + a nonce + issuance time, checked for a short expiry on callback) — this is a new, small piece of security-sensitive code that didn't exist before (Clerk's login flow gets this for free from the `AddOAuth` middleware; this hand-rolled flow does not, and must implement it deliberately).

## Risks / Trade-offs

- [Hand-rolling an OAuth exchange is more code to get right than reusing middleware, including the state/CSRF handling middleware normally provides for free] → Mitigation: the flow is intentionally small (one redirect out, one callback, one API call, discard) — less code overall than fighting `AddOAuth`'s session-oriented design, and the state parameter's job is well-understood and testable in isolation.
- [A customer could start a connection flow, abandon it, and start another — needs the callback to handle a `state` that's expired or doesn't match any pending flow gracefully, not crash] → Mitigation: treat an invalid/expired `state` as a plain redirect back to the dashboard with a "connection attempt expired, try again" message, not an unhandled exception.
- [GitHub API rate limits apply to the repo-listing call] → Mitigation: this is a customer-triggered, one-shot call per connection attempt, not a background poller — realistic volume is far below GitHub's per-token rate limits; revisit only if it becomes a real problem.
