## Context

See proposal.md for why. Relevant existing shape: `GitHubConnectionFlowService.BuildAuthorizeUrl`
reads `GitHubConnection:ClientId`/`CallbackUrl` from `IConfiguration` and sends `redirect_uri`
verbatim to GitHub; GitHub rejects the authorize request outright if it doesn't exactly match the
one registered callback URL for that Client ID. The production OAuth App
(`hosted-platform-deployment` group 8) is registered against the real Vercel URL and must not be
touched by this change. `web/cypress.config.ts` already loads local secrets via `dotenv` from
`.env.local` (Clerk) and already has a precedent for a Node-side task calling an external API with
a server-side secret (`ensureE2ETestUser`/`createSignInTicket` calling Clerk's API).

## Goals / Non-Goals

**Goals:**
- Exercise the real, unmocked GitHub OAuth authorize → consent → callback → repo-list → confirm
  path against real `github.com`, using the project owner's real account and a real repo
  (`ernestoalejowitt22/NAHA`).
- Keep the project owner's real GitHub password and TOTP secret out of any file that lives in the
  repo (even gitignored) — sourced from AWS Secrets Manager at run time instead.
- Leave the production OAuth App and its configuration completely untouched.

**Non-Goals:**
- No CI wiring for this spec in this change — it's designed to run locally first (where "AWS
  credentials already available in the environment" is the developer's own configured profile);
  running it in GitHub Actions is a separate follow-up once the local path is proven, since CI would
  need its own AWS credential story (e.g. OIDC, matching `deploy-hosted.yml`'s pattern) to reach the
  same secret.
- No handling for GitHub's account-level device/new-IP email verification challenge (independent of
  2FA, not scriptable) — see Risks below.
- No change to `GitHubConnectionFlowService`, `ConnectionEndpoints`, or any other application code.

## Decisions

**A second OAuth App, registered manually by the project owner, callback
`http://localhost:3000/connect/github/callback`.** GitHub OAuth Apps allow exactly one callback URL
per app, so the existing production app can't serve local runs — see proposal.md.

**Both the OAuth App's Client ID/Secret/CallbackUrl and the GitHub account's password/TOTP secret
live in AWS Secrets Manager** — revised from an earlier draft of this design, which planned to pass
the OAuth App's config as plain local env vars (reasoning: it's this app's own configuration, the
same category of value as the *production* Client ID/Secret, which already live as plain GitHub
Actions repo secrets — see `hosted-platform-deployment` task 8.2). That would have required the
project owner to hand the Client ID/Secret to whoever runs the spec (in this case, pasting them into
chat), which defeats the point of keeping secrets out of the conversation entirely. Putting both in
the same secrets service removes that hand-off: anyone (or any automation) with the already-assumed
ambient AWS credentials can run the full spec without a human relaying values through another
channel. Two secrets, not one, since they're conceptually different (app config vs. personal account
access) and may need different access scoping later:
- `releasetwin/e2e/github-oauth-app`: `{"clientId": "...", "clientSecret": "...", "callbackUrl":
  "http://localhost:3000/connect/github/callback"}` — fetched by a new
  `web/scripts/e2e-api-with-github.mjs`, which then launches `dotnet run` with those values as
  `GitHubConnection__*` env vars (same env-var-shape convention `Program.cs` already reads). Wired
  into `web/package.json` as `e2e:api:github`/`e2e:github` (a separate script from the existing
  `e2e`/`e2e:api`, so the other specs don't newly depend on this secret existing).
- `releasetwin/e2e/github-account`: `{"username": "...", "password": "...", "totpSecret": "..."}` —
  fetched by a new `fetchGitHubTestAccount` Cypress task (`cypress.config.ts`, alongside
  `ensureE2ETestUser`), which also generates the current TOTP code from `totpSecret` at call time
  (via `otplib`) — codes are time-windowed and can't be pre-generated or cached.

**Login automation via `cy.origin('https://github.com')`.** Cypress's cross-origin support is
required here since the OAuth flow genuinely navigates the browser to `github.com` — this isn't
optional the way it might be for a same-origin form.

**The e2e spec asserts against the real, specific repo `ernestoalejowitt22/NAHA`.** Not a
newly-created throwaway repo — the point of this change is proving the flow against something real,
per the project owner's own framing ("customer perspective").

## Risks / Trade-offs

- **GitHub's device/new-IP "verify this sign-in" email challenge is independent of 2FA and has no
  scriptable answer.** → Accepted: expect occasional flake on a genuinely new IP/browser
  fingerprint (most likely in CI, which this change explicitly doesn't wire up yet); running
  repeatedly from the same local machine keeps the fingerprint recognized most of the time.
- **TOTP codes are time-windowed (typically 30s) and can drift near a boundary.** → Mitigate by
  generating the code as late as possible (immediately before typing it in), not at test start.
- **Storing a personal account's real password + TOTP secret anywhere automatable is a materially
  larger blast radius than the disposable-account alternative that was explicitly considered and
  rejected here.** → Accepted, by the project owner's explicit choice, with the AWS Secrets Manager
  placement specifically chosen to reduce (not eliminate) that exposure surface.
- **If GitHub changes its login/consent page markup, the spec breaks** — same maintenance risk any
  third-party-UI automation carries, no different in kind from what a disposable account would have.

## Migration Plan

None — this is entirely new test tooling with no effect on any deployed system. Registering the
second OAuth App and creating the AWS secret are manual, one-time setup steps the project owner
performs before the spec can run; nothing here is reversible/rollback-relevant in the way a
production change would be.
