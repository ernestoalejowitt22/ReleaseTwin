## Why

A pre-pilot security exploration of the hosted API (2026-09-01) surfaced a cluster
of tenant-isolation, abuse-resistance, and auth-hardening gaps. None is a
known-exploited hole today, but each is the kind of finding a pilot customer's
security review will raise, and several (invite email binding, blob key
namespacing) are cheap to close now and awkward to retrofit after real tenant
data exists.

## What Changes

- **Invite acceptance binds to a verified email.** `OrganizationMembersService.AcceptAsync`
  gains the `invitation.Email == user.Email` check that `ProvisioningService`
  intends but cannot currently enforce; the `email is null ⇒ allow` bypass is
  removed. Requires a Clerk JWT template that carries a verified `email` claim
  (manual Clerk-console step — see Impact).
- **Evidence blob keys are validated and namespaced.** The ingest path rejects a
  `screenshot:<id>` part whose id is not `^[0-9a-f]{32}$`, and blob store keys are
  prefixed with the owning `projectId` so one project's token can never overwrite
  or collide with another project's screenshot or an export archive.
- **Abuse rate limiting on the public and auth surface.** New per-caller limits on
  `/api/ingest/*`, the anonymous `/api/shared-runs/*` routes, and the billing
  webhook, sized so honest CI and share-link traffic is unaffected. **BREAKING**
  for a client that hammers ingest — it now gets `429` instead of unbounded
  acceptance. The Lambda Function URL is `authorization_type = NONE` with no
  CloudFront/WAF in front, so design.md weighs an in-process ASP.NET rate limiter
  vs. fronting the function URL with CloudFront + WAF rate rules.
- **Billing webhook rejects stale timestamps.** `BillingWebhookSignature.Verify`
  additionally requires `webhook-timestamp` within a ±5-minute tolerance, per the
  Standard Webhooks scheme, closing the replay window that idempotency only
  partially covers.
- **Notification dispatch pins the validated IP.** The SSRF check result is
  carried through to the HTTP connection (`SocketsHttpHandler.ConnectCallback`) so
  a 0-TTL DNS record cannot rebind between validation and connect.
- **Clerk JWT audience is validated.** Once the JWT template above exists, the
  handler sets `ValidateAudience = true` against this API's audience.
- **GitHub connection `state` is bound to the initiating user.** The
  DataProtection-protected `state` payload includes the Clerk `user_id`;
  `/callback` rejects a state minted for a different user.

## Capabilities

### New Capabilities
- `abuse-rate-limiting`: per-caller request limits on the ingest path, the
  anonymous share-link routes, and the billing webhook; limit dimensions,
  thresholds, the `429` contract, and what is explicitly exempt.

### Modified Capabilities
- `org-membership`: an invitation SHALL only be acceptable by an authenticated
  user whose verified email matches the invited address; the invitation-preview
  endpoint SHALL NOT disclose the invited email address to an arbitrary
  link-holder.
- `ingest-api`: a screenshot id MUST match `^[0-9a-f]{32}$`; evidence blob storage
  MUST namespace keys by project so cross-project overwrite is impossible.
- `evidence-store`: blob keys are project-namespaced (retrieval and purge paths
  updated to match).
- `billing`: webhook verification MUST reject a request whose `webhook-timestamp`
  is outside a bounded tolerance.
- `run-notifications`: the outbound webhook connection MUST target the same IP the
  SSRF validator approved, not a re-resolved one.
- `account-provisioning`: the Clerk session JWT MUST be validated for audience;
  provisioning MUST receive a verified email claim.
- `project-connections`: the OAuth `state` MUST be bound to the user who started
  the flow.

## Impact

- **Code:** `hosted/ReleaseTwin.Hosted.Api/` — `Services/OrganizationMembersService.cs`,
  `Services/ProvisioningService.cs`, `Endpoints/IngestEndpoints.cs`,
  `Services/EvidenceIngestService.cs`, `Data/Store/S3EvidenceBlobStore.cs` +
  `FileSystemEvidenceBlobStore`, `Services/EvidencePurgeService.cs`,
  `Billing/BillingWebhookSignature.cs`, `Services/NotificationDispatchService.cs`,
  `Program.cs` (JWT options, rate limiter), `Services/ConnectionStateService.cs`,
  `Services/GitHubConnectionFlowService.cs`.
- **Data model:** new blob key format. A migration/back-compat read path is needed
  for evidence uploaded under the old flat keys, or a one-time re-key — decide in
  design.md.
- **Infra (`hosted/terraform/`):** enabling S3 versioning on the evidence bucket is
  a cheap mitigating control to pair with key-namespacing (an overwrite is
  currently irreversible — no versioning today). Moving `Polar__WebhookSecret` /
  `GitHubConnection__ClientSecret` from plaintext Lambda env vars to Secrets
  Manager references is a related enterprise-review item — fold in or split out in
  design.md. Per-task Lambda IAM roles are already well isolated (verified) — no
  change needed there.
- **Manual steps (unavoidable, Clerk console):** create a Clerk JWT template that
  includes a verified `email` claim and an audience for this API. Without it, the
  invite-email and audience items stay dark behind a flag. Everything else is
  code-side.
- **Tests:** `hosted/ReleaseTwin.Hosted.Api.Tests/` — new cases for each item;
  existing invite/ingest/webhook tests updated.
- **Out of scope:** server-side evidence redaction backstop, GitHub App migration
  (finding #10), and the broader `repo` OAuth scope — tracked separately.
