## 1. Data model

- [x] 1.1 Add `Connection` entity (`Id`, `ProjectId`, `Provider`, `ExternalRepo`, `ConnectedAt`) to `hosted/ReleaseTwin.Hosted.Api/Data/Entities/`.
- [x] 1.2 Add `DbSet<Connection>` to `HostedDbContext`, a unique index on `ProjectId` (one connection per project), and a foreign key to `Project`.

## 2. State/CSRF handling

- [x] 2.1 Implement a small service to mint and validate a signed `state` value encoding `ProjectId` + a nonce + issuance time, with a short expiry (server-secret HMAC, not a bare/guessable string).
- [x] 2.2 Add a test: a tampered or expired `state` is rejected, not silently trusted.

## 3. Connection flow (hand-rolled, not AddOAuth)

- [x] 3.1 Add a "Connect GitHub" action on the dashboard (per-project) that redirects to GitHub's authorize endpoint with the signed `state`, requesting the narrowest scope that lists repositories (verify during implementation whether public-repo scope suffices or private-repo visibility is needed).
- [x] 3.2 Add a callback handler that validates `state`, exchanges the authorization code for an access token via a direct `HttpClient` POST to GitHub's token endpoint (not ASP.NET Core auth middleware), and calls GitHub's repo-list API with that token held only in a local variable.
- [x] 3.3 Render the picker (repo names) in the same response as the callback — the confirming POST carries only the chosen repo identifier forward, never the token.
- [x] 3.4 Add a handler that saves the chosen repo as a `Connection` for the project, scoped to the signed-in customer's own organization (same `ProjectBelongsToCurrentOrgAsync`-style check `DashboardModel` already uses for tokens).
- [x] 3.5 Add a test proving the access token variable's lifetime never leaves the repo-listing method — e.g. by construction (no field, no cache, no session write) plus a test asserting no `Connection` row or any other persisted record ever contains anything token-shaped.

## 4. Dashboard UI

- [x] 4.1 Show the connected repo (if any) next to each project on the dashboard.
- [x] 4.2 Add a "Disconnect" action that deletes the project's `Connection` row, scoped to the signed-in customer's own organization.
- [x] 4.3 Handle an expired/invalid `state` on callback gracefully — redirect to the dashboard with a "connection attempt expired, try again" message, not an unhandled exception.

## 5. Tests

- [x] 5.1 Unauthenticated connection start/callback/confirm is denied, consistent with other dashboard actions.
- [x] 5.2 A connection attempt for a project outside the signed-in organization is rejected.
- [x] 5.3 Disconnecting removes the displayed link and the underlying row.
- [x] 5.4 `dotnet test ReleaseTwin.Hosted.slnx` clean, no live GitHub OAuth App required to run the suite (matches the existing pattern for Clerk/Azure DevOps credentials in tests).

## 6. Docs

- [x] 6.1 Document the new GitHub OAuth App requirement (distinct from any GitHub option Clerk itself might offer) in README.md/`docs/installation-model.md`, including its required scope and callback URL.

## 7. Manual verification (requires a real GitHub OAuth App — outside this repo)

- [ ] 7.1 Register a GitHub OAuth App for this purpose, set its callback URL, supply Client ID/Secret via environment variables, and confirm connect → pick a real repo → see it on the dashboard → disconnect, end to end.
