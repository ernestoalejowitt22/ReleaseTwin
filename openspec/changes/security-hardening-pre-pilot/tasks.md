## 1. Web-session token audience + verified email (D1)

- [x] 1.1 Add `Clerk:Audience` config binding in `Program.cs`; when set, `TokenValidationParameters.ValidateAudience = true` with `ValidAudience` from it, else leave `false`. Emit one startup log line stating which mode is active.
- [x] 1.2 In the `OnTokenValidated` hook, keep reading `email` when present (already partially there) and ensure it flows to `ProvisioningService.GetOrCreateUserAsync` as the verified email; confirm a token with no `email` claim yields a null stored email (not empty string).
- [x] 1.3 Wire a `CLERK_AUDIENCE` repo variable through `deploy-hosted.yml` → `hosted/terraform/lambda.tf` env var `Clerk__Audience` (default empty, safe).
- [x] 1.4 Tests: valid token for this audience is accepted; correctly-signed token for another audience is rejected as unauthenticated and provisions nothing; audience-unset mode still accepts a no-`aud` token (back-compat). _(Tests assert the config→options wiring via `IOptionsMonitor<JwtBearerOptions>`; the accept/reject behavior itself is ASP.NET JwtBearer's, not ours — `ClerkJwtAudienceOptionsTests`.)_
- [ ] 1.5 **Needs the user to run this (Clerk console):** create a Clerk JWT template exposing a provider-verified `email` claim and an `aud` of this API; then set the `CLERK_AUDIENCE` repo variable. Verify with a real login e2e that `email` and `aud` appear on the session token.

## 2. Invitation acceptance binds to verified email (D2)

- [x] 2.1 `OrganizationMembersService.AcceptAsync`: after loading the invitation, throw `InvitationInvalidException` (the same exception/shape as expired) when the caller's stored verified email is null or does not case-insensitively equal `invitation.Email`.
- [x] 2.2 `ProvisioningService.IsJoiningByInviteAsync`: remove the `email is null` early-return-true branch so the signup-join path matches 2.1.
- [x] 2.3 Invitation-preview endpoint (`GET /api/invitations/{token}`): stop returning the invited `email` field; keep organization name + role. Update `web/src/app/invitations/[token]/page.tsx` and its `InvitePreview` type to drop `email`.
- [x] 2.4 Tests: matching verified email accepts and consumes the invite; non-matching email is refused with a response identical to an invalid/expired invite (no membership, no disclosure); missing verified email is a non-match, not a bypass; role stays fixed at the invited role; preview response contains no email.

## 3. Screenshot id validation + project-namespaced blob keys (D3)

- [x] 3.1 `IngestEndpoints.ReadAsync`: reject the whole multipart upload with `400` (nothing stored) if any `screenshot:<id>` part's id fails `^[0-9a-f]{32}$`. Apply to both `/case-report` and `/flag-proof-report`. _(Shared `ReadAsync<T>` covers both; `ScreenshotId.IsValid` is the single definition.)_
- [x] 3.2 Change `IEvidenceBlobStore` methods to take the owning `projectId` (or a composed key); update `S3EvidenceBlobStore` and `FileSystemEvidenceBlobStore` to key as `evidence/{projectId}/{screenshotId}`. _(Keys are `screenshots/{projectId:N}/{id}` for S3, `{projectId:N}/{id}.png` for FS.)_
- [x] 3.3 Update every caller — `EvidenceIngestService.StoreAsync`, `EvidenceSharingService.ResolveScreenshotAsync`, the dashboard screenshot proxy path, `EvidencePurgeService` — to pass the report's own `ProjectId` (never a client value). _(+ `ExportArchiveBuilder`, which now carries `ProjectId` per screenshot.)_
- [x] 3.4 Read/purge fallback: when the namespaced key misses, retry the legacy flat key so evidence uploaded before this change still resolves. Add a short code comment marking this as removable in a later change.
- [x] 3.5 Tests: a non-hex / path-separator / uppercase / wrong-length id rejects the whole upload; a project-A upload whose id collides with a stored project-B blob does not touch B; retrieval still requires access to the owning report; purge deletes only the target project's namespace; legacy flat-key evidence still reads. _(`ScreenshotIdAndBlobNamespacingTests`; report-access gate unchanged — `EvidenceStoreTests` / `EvidenceSharingTests` still green.)_
- [x] 3.6 `hosted/terraform/evidence.tf`: enable S3 versioning on the evidence bucket (+ noncurrent-version expiry for both prefixes). **Needs the user to run this:** confirm the auto-apply succeeded post-merge (`aws s3api get-bucket-versioning`).

## 4. Billing webhook timestamp tolerance (D4)

- [x] 4.1 `BillingWebhookSignature.Verify`: after the HMAC match, parse `webhook-timestamp` as Unix seconds and return `false` when `|now - ts|` exceeds a 5-minute tolerance (constant, documented). _(Freshness checked before the HMAC; `now` is an injectable param defaulting to `DateTimeOffset.UtcNow`; `TimestampTolerance` is public for tests.)_
- [x] 4.2 Confirm the caller path treats a `false` from `Verify` the same for stale-timestamp as for bad-signature: non-2xx, nothing recorded in `ProcessedBillingEventRepository`, no state change. _(`BillingEndpoints` returns 401 before `processor.ProcessAsync`; comment added.)_
- [x] 4.3 Tests: correct signature + fresh timestamp passes; correct signature + timestamp older than tolerance is rejected and not recorded processed; correct signature + far-future timestamp is rejected; missing/garbage timestamp is rejected. _(`BillingEventProcessorTests.SignatureVerificationRejectsStaleAndFutureTimestamps` + HTTP `StaleButValidlySignedWebhookIsRejectedAndNotProcessed`.)_

## 5. Notification dispatch pins the validated address (D5)

- [x] 5.1 Extend `OutboundUrlValidator` (or add a sibling) to return the approved `IPAddress[]` alongside the allow/deny result, without changing the existing `IsAllowed` signature used at save time. _(New 4-arg overload with `out IPAddress[] approvedAddresses`; the 3-arg one delegates to it.)_
- [x] 5.2 `NotificationDispatchService`: build its `HttpClient` on a `SocketsHttpHandler` with a `ConnectCallback` that dials the approved IP (first usable address) on the URL's port, leaving TLS SNI/Host as the original hostname. Keep `AllowAutoRedirect = false` and the 5s timeout. _(Callback `ConnectToPinnedAddressAsync` reads `PinnedAddressOption` off the request; wired in Program.cs.)_
- [x] 5.3 Re-run the SSRF check immediately before dispatch (already present) and pass its approved address into the connect path; on deny, record the delivery as failed with the reason and send nothing.
- [x] 5.4 Tests (injected resolver): host that passes at save time but resolves to a private address at send time results in no connection and a recorded failure; a normal public host still delivers; redirects are still not followed. _(`RevalidatesTargetUrlAtSendTimeAndSkipsPrivateAddress` pre-existing + `DeliveryPinsTheValidatedAddressOnTheRequest`; redirect-off is a Program.cs handler setting, unchanged.)_

## 6. OAuth state bound to user (D6)

- [x] 6.1 `ConnectionStateService.Mint(projectId, userId)` protects `"{projectId}:{userId}"`; `Validate` returns both (or null on parse/crypto failure). Keep the time-limited protector and lifetime.
- [x] 6.2 `ConnectionEndpoints`/`GitHubConnectionFlowService`: pass the current Clerk `user_id` into the authorize (mint) and callback (validate) steps; callback returns the generic "expired or invalid" result on user mismatch. _(Both endpoints now take `ClaimsPrincipal`; a missing/unparseable `user_id` is `Forbid`/`BadRequest`.)_
- [x] 6.3 Tests: a state minted for user A is rejected when presented by user B (even same org); expired/altered/unknown state gives the same generic refusal; the happy path (same user) still returns the repo list. _(`StateMintedForAnotherUserIsRejected` + updated `ConnectionStateServiceTests`/`ConnectionFlowTests`.)_

## 7. Rate limiting (D7)

- [x] 7.1 Add `Microsoft.AspNetCore.RateLimiting` middleware in `Program.cs` with a `RateLimiting:Enabled` config kill-switch (default on) and partitioned limiters: ingest by API-token hash (token bucket), `/api/shared-runs/*` by client IP (fixed window), `/api/billing/webhook` by client IP (fixed window, ordered before the endpoint body). _(`RateLimiting.cs`; token hash is a new `token_hash` claim on the ApiToken principal. `app.UseRateLimiter()` sits after auth, before endpoints.)_
- [x] 7.2 `OnRejected`: return `429` with `Retry-After`; on limiter-store failure, log and allow the request (fail open for the platform). _(429 via `RejectionStatusCode`; `Retry-After` from lease metadata; `QueueLimit = 0` so no queuing.)_
- [x] 7.3 Pick concrete ceilings: size the ingest bucket against the largest supported case-suite size × realistic CI parallelism + retries (document the arithmetic in a comment); size the share-link window so a shared page with the max screenshot count loads fully. _(Ingest: 5,000 burst + 50/s sustained; share links: 120/min/addr; webhook: 60/min/addr. Arithmetic in the `RateLimiting` class doc. All config-overridable.)_
- [x] 7.4 Client IP source: take the left-most `X-Forwarded-For` entry populated by the Function URL; add a code comment noting the fallback to connection id if spoofing abuse appears.
- [x] 7.5 Tests: a per-token burst past the ceiling gets `429` + `Retry-After` with no report/evidence/usage recorded; a full max-size suite upload is never throttled; token A throttling does not affect token B; a share-page load with max screenshots is never throttled; webhook flood is rejected before signature verification; `RateLimiting:Enabled=false` disables all of it. _(`RateLimitingTests`, 6 tests, tiny config-driven limits.)_

## 8. Verification

- [x] 8.1 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green; report the new test count and the delta. _(Engine: 253/253, unchanged — no engine code touched. Hosted (`hosted-ci.yml`, not in the .sln): 374/374, +26 new tests. Solution + hosted both build clean.)_
- [x] 8.2 `cd web && npm run build && npx eslint` green (covers the invitation-preview type change). _(`next build` clean; `npx eslint .` exit 0.)_
- [x] 8.3 `openspec validate security-hardening-pre-pilot --strict` passes.
- [x] 8.4 Confirm no pilot invitations are outstanding before merge (D2 makes them un-acceptable until the Clerk template exists); note this in the PR description. _(Per project memory no pilots are onboarded yet; to be restated in the PR body. Also: until `CLERK_AUDIENCE` + the email-bearing JWT template exist, invite acceptance treats every attempt as a non-match — the Clerk step (8.5) should land promptly after merge.)_
- [ ] 8.5 **Needs the user to run this:** after merge + auto-deploy, do the Clerk console step (1.5), set `CLERK_AUDIENCE`, verify S3 versioning (3.6), and smoke-test one real invite acceptance end to end.
