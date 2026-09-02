## Context

See `proposal.md` — Why. Seven independent hardening items land in one change
because they share a review context, a test project
(`hosted/ReleaseTwin.Hosted.Api.Tests`), and a deploy. They do not share code.
Constraints that shape the approach:

- **Deploy is CI-only** (OIDC + Terraform in `deploy-hosted.yml`); no local
  `terraform apply`. Any infra change ships as `hosted/terraform/**` edits that
  auto-apply on merge.
- **Auth today**: `ClerkJwt` (BFF) and `ApiToken` (CLI) schemes, `Program.cs`.
  Clerk's default session token carries no `email` and no API-specific `aud`
  (Program.cs:302-304 comments). Adding either requires a **Clerk JWT template**,
  configured in the Clerk dashboard — no code path creates it.
- **Blob store**: `IEvidenceBlobStore` — `S3EvidenceBlobStore` (prod) and
  `FileSystemEvidenceBlobStore` (dev). Key today is the bare screenshot id.
  Export archives live in the *same* bucket under `exports/`. No S3 versioning.
- **Billing idempotency**: `ProcessedBillingEventRepository`, DynamoDB item per
  Polar event id, 30-day TTL. Signature check in `BillingWebhookSignature.Verify`
  already parses `webhook-timestamp` but ignores its value.
- **SSRF guard**: `OutboundUrlValidator.IsAllowed` resolves the host and checks
  every address; `NotificationDispatchService` then hands the URL to a pooled
  `HttpClient` that resolves again independently.
- **Runtime**: .NET 10 on Lambda. `Microsoft.AspNetCore.RateLimiting` and
  `SocketsHttpHandler.ConnectCallback` are both in-box.

## Goals / Non-Goals

**Goals**

- Close each finding with the smallest change that satisfies its spec, keeping
  the two auth schemes and the BFF boundary intact.
- Ship the two Clerk-template-dependent items (audience, invite-email) behind a
  runtime check so the code merges and deploys before the template exists, then
  activates when it does — no second deploy needed.
- Keep honest CI ingest and shared-page loads provably under every new limit.

**Non-Goals**

- Server-side evidence re-redaction, GitHub App migration, broader `repo` scope
  narrowing — tracked separately (proposal "Out of scope").
- A distributed/shared rate-limit store. Per-instance limiting is accepted (see
  Decisions).
- Moving billing/GitHub secrets to Secrets Manager — noted in proposal Impact as
  a related item; fold in only if it costs nothing extra during the webhook work,
  otherwise leave for its own change.
- Re-keying evidence blobs already stored under the flat key (see Migration).

## Decisions

### D1 — Clerk JWT template carries `email` + `aud`; code activates conditionally

Add one Clerk JWT template exposing a **verified** `email` claim and an `aud` of
this API. In `Program.cs`:

- `ValidateAudience` becomes `true` **only when** `Clerk:Audience` config is set;
  unset ⇒ current behaviour (issuer/signature/expiry only), logged once at
  startup as "audience validation disabled".
- The `OnTokenValidated` provisioning hook reads `email` when present and stores
  it (it already has a fallback path).
- Invite acceptance (D2) keys off the stored verified email.

**Why conditional, not a feature flag:** the existing flag seam
(`IFlagService`) is for product features with a registry entry and tests; this is
a deploy-ordering concern (template before enforcement) that a plain config
presence check expresses more honestly and removes once the template is live.

**Alternative rejected:** require the template before merging. Couples a code PR
to a manual dashboard step with no rollback story; the conditional path is a few
lines and self-documents the transition.

### D2 — Invite acceptance: verified-email equality, refusal == invalid-invite

`OrganizationMembersService.AcceptAsync` gains: load invitation → if
`invitation.Email` does not case-insensitively equal the caller's stored verified
email (or the caller has none) → throw the same `InvitationInvalidException` an
expired invite throws. Remove the `email is null ⇒ allow` branch in
`ProvisioningService.IsJoiningByInviteAsync` (the signup-join path) so both paths
agree. The invitation-preview endpoint (`GET /api/invitations/{token}`, consumed
by `web/src/app/invitations/[token]/page.tsx`) stops returning `email`.

**Why identical error:** an attacker probing a forwarded link learns nothing
about who was invited or whether the token is real.

**Alternative rejected:** verify against Clerk's user API at accept time instead
of a stored claim. Extra network dependency on the hot path for no gain — the JWT
claim is already provider-verified.

### D3 — Screenshot id validation at the ingest boundary + project-namespaced keys

Two layers:

1. **Boundary check** in `IngestEndpoints.ReadAsync`: reject the whole multipart
   upload (`400`, nothing stored) if any `screenshot:<id>` part's id fails
   `^[0-9a-f]{32}$`. This is the spec's normative check.
2. **Namespaced keys**: `EvidenceIngestService.StoreAsync` and every read/purge
   path compute the blob key as `evidence/{projectId}/{screenshotId}` (or pass
   `projectId` into `IEvidenceBlobStore` and let the store compose it — decided
   in tasks; the interface change is small and there are two implementers). The
   stored `UploadedRunEvidence.ScreenshotIds` stays as bare ids; the key is
   derived at call time from the report's own `ProjectId`, so a caller can never
   influence which project's namespace is touched.

`exports/` and `evidence/` prefixes now partition the bucket cleanly.

**Enable S3 versioning** on the evidence bucket (`hosted/terraform/evidence.tf`)
as the mitigating control the spec's "cannot overwrite" requirement leans on for
defence in depth — cheap, and makes an accidental future collision recoverable.

**Alternative rejected:** sanitize/encode the id instead of rejecting. Hides
client bugs and leaves the "is this 32-hex" invariant implicit; an explicit
`400` is a contract.

### D4 — Billing webhook timestamp tolerance

In `BillingWebhookSignature.Verify` (or its caller), after the HMAC check,
parse `webhook-timestamp` as Unix seconds and reject when
`|now - ts| > tolerance`. Tolerance = **5 minutes**, matching the Standard
Webhooks recommendation and comfortably inside Polar's clock skew. A rejected
stale event returns non-2xx and is **not** recorded in
`ProcessedBillingEventRepository` (same as a bad signature).

**Why in `Verify`:** keeps "is this request authentic and fresh" in one testable
function; the endpoint stays a thin caller.

**Alternative rejected:** rely on idempotency alone. The 30-day TTL means a
captured "subscription active" event replayed on day 31 re-applies — small window,
but the fix is three lines.

### D5 — Notification dispatch pins the validated address

`NotificationDispatchService` builds its `HttpClient` with a
`SocketsHttpHandler` whose `ConnectCallback` opens the socket to the **IP the
validator already resolved and approved**, not to a fresh lookup of the host.
Flow: `OutboundUrlValidator` is extended (or a sibling returns the approved
`IPAddress[]`), dispatch picks one, `ConnectCallback` dials it while TLS SNI/Host
stays the original hostname. Redirects remain disabled; 5s timeout unchanged.

**Why not a custom resolver / `HttpClient.DangerousAcceptAnyServerCertificate`:**
none needed — `ConnectCallback` dials an `IPEndPoint` and the default TLS
validation still runs against the SNI host.

**Alternative rejected:** re-validate the freshly-resolved IP inside a
`DelegatingHandler` before the send. Still a TOCTOU (resolution in the handler vs.
in the socket connect); pinning removes the gap entirely.

### D6 — OAuth `state` bound to user

`ConnectionStateService.Mint` takes the Clerk `user_id` and protects
`"{projectId}:{userId}"` (still via the time-limited DataProtection protector).
`Validate` returns both; `GitHubConnectionFlowService.ExchangeCodeForRepositoriesAsync`
is passed the current `user_id` and returns `null` (generic "expired or invalid")
on mismatch. `/confirm`'s existing org-ownership check is unchanged — this closes
the `/callback` gap, not a persistence gap.

### D7 — Rate limiting: in-process, per-instance, ASP.NET `RateLimiter`

Use `Microsoft.AspNetCore.RateLimiting` with partitioned limiters:

| Surface | Partition key | Shape |
|---|---|---|
| `/api/ingest/*` | API token hash (from the auth ticket) | token bucket, generous — sized in tasks against the largest supported suite |
| `/api/shared-runs/*` | client IP (`X-Forwarded-For` left-most, Function URL sets it) | fixed window |
| `/api/billing/webhook` | client IP | fixed window, applied **before** the endpoint body so signature verification isn't reached |

Rejections return `429` + `Retry-After`. Limiter partition-store failure ⇒
`OnRejected` logs and the request is allowed (spec: fails open for the platform).

**Why per-instance, not DynamoDB/Redis-backed:** Lambda concurrency for this
workload is low; a per-instance limiter still bounds total throughput to
`(instances × ceiling)` which is far below what a flood needs to cost real money,
and it adds zero latency and zero new infra. If abuse proves it insufficient, the
follow-up is CloudFront + WAF in front of the Function URL (rate-based rules) —
noted below, deliberately not done now.

**Alternative rejected:** CloudFront + WAF now. Real cost, a new distribution and
cert story in front of a URL that four Lambda functions and the BFF all target,
and WAF rate rules are coarse (5-minute windows, per-IP only). The in-process
limiter is more precise for the token-partitioned ingest case and ships in this
change; WAF stays a documented escalation.

## Risks / Trade-offs

- **Clerk template misconfiguration** (wrong claim name, `email` not marked
  verified) → audience/invite items silently stay dark or, worse, enforce against
  an empty value. Mitigation: startup log line stating which mode is active; a
  test asserting "no verified email ⇒ treated as non-match, never as allow"; a
  manual verification step in tasks after the template is created.
- **Per-instance rate limiting under-counts** a distributed flood across many
  warm Lambda instances. Mitigation: ceilings are set low enough that
  `instances × ceiling` is still cheap; WAF escalation documented.
- **`X-Forwarded-For` spoofing** for the IP-partitioned limiters — a client can
  forge the header and rotate "IPs". Mitigation: Function URL populates XFF from
  the real edge connection; take the left-most entry and, if abuse appears,
  switch the share/webhook partitions to the Function URL's connection id. The
  ingest limiter (the one that matters most) is token-partitioned, not
  IP-partitioned, so it is unaffected.
- **Namespaced blob keys strand old evidence** if a read path is missed.
  Mitigation: see Migration — reads fall back to the flat key for a bounded
  period; a test covers both.
- **`ConnectCallback` + corporate TLS-inspection proxies**: pinning the IP is
  fine, but if a customer routes egress through a proxy by hostname, dialing the
  resolved IP bypasses it. Acceptable — notification webhooks are expected to be
  public HTTPS endpoints, and the SSRF guard already requires a public address.

## Migration Plan

1. **Merge + auto-deploy** the code. All items except audience-validation and
   invite-email enforcement are live immediately. Audience stays disabled
   (`Clerk:Audience` unset); invite acceptance, until a verified email is
   present, treats every acceptance as a non-match — **so create the Clerk
   template promptly** or pending invites cannot be accepted. (Acceptable: no
   pilot invites are outstanding yet — confirm before merge.)
2. **Terraform** applies S3 versioning on the evidence bucket in the same deploy.
3. **Blob keys**: new uploads use `evidence/{projectId}/{id}`. Read and purge
   paths try the namespaced key, then fall back to the flat key. Given evidence
   is Paid-tier and retention-windowed (≤365d, default lower), the flat-key
   fallback can be removed in a later change once no flat-key evidence remains —
   or a one-off re-key script run then. No blocking migration now.
4. **Clerk dashboard** (manual, unavoidable): create the JWT template with a
   verified `email` claim and this API's `aud`. Then set the `CLERK_AUDIENCE`
   repo variable → next deploy flips `ValidateAudience` on. Verify with a real
   token (an existing e2e covers the login path).
5. **Rollback**: each item is independently revertable. The rate limiter can be
   disabled via config (`RateLimiting:Enabled=false`) without a code change.

## Open Questions

- Exact ingest ceiling numbers — resolved in tasks against the real max suite
  size and CI parallelism; does not affect specs or approach.
- Whether to fold the billing/GitHub-secret → Secrets Manager move into this
  change or split it — decide when the webhook work starts; it does not affect
  any spec here.
