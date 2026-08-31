## 1. Membership data model + migration (design D1–D3)

- [x] 1.1 Add `Membership` and `Invitation` entities (`Data/Entities`) and their DynamoDB item shapes: `PK=ORG#<orgId>`, `SK=MEMBER#<userId>` / `SK=INVITE#<token>`, with `role`, `email`, `expiresAt`, `state`, `createdAt`
- [x] 1.2 ~~Add the reverse-lookup GSI in `hosted/terraform/`~~ — **not needed**: membership items reuse the existing overloaded `GSI1` (`GSI1PK=USER#<userId>`, `GSI1SK=ORG#<orgId>`), a key namespace no other GSI1 writer uses. No Terraform change.
- [x] 1.3 Add `MembershipRepository` (+ interface in `IRepositories.cs`): `ListMembersByOrg`, `ListOrgsByUser`, `GetMembership`, `Put`, `Delete`
- [x] 1.4 Add `InvitationRepository`: `Put`, `GetByToken` (token encodes `<orgId>.<random>`), `ListByOrg`, `Delete`; single-use enforced by an atomic `INVITECLAIM#` marker item in the `ClaimAsync` transaction (InMemory fake only supports `attribute_not_exists`, so a claim marker beats a `state` condition-expression)
- [x] 1.5 Load-time read-repair (`MembershipService`): a user with no `Membership` item but a legacy `AppUser.OrganizationId` is synthesized a founding `Admin` membership and it is persisted
- [x] 1.6 `AppUser.OrganizationId` kept write-through on new-org signup (`CreateWithOrganizationAsync` 3-item transaction); invite-join signup leaves it `Guid.Empty`
- [x] 1.7 Unit tests: `MembershipRepositoryTests` (7) — both-way listing, read-repair persists, no-op without legacy org, token encodes org id, single-use claim rejects a second user, list/revoke, acceptability

## 2. Active organization + access guard (design D3–D4)

- [x] 2.1 Active org resolved per request in `OnTokenValidated`: the BFF sends `X-Org-Id`, honoured only if the caller is a member of it, else their default org (earliest membership); stamped as `org_id` + `org_role` claims. No cookie needed — the JWT is minted by Next.js per request so the header is sufficient and switch-friendly.
- [x] 2.2 `CurrentOrganizationAccessor` gains `Role` (from `org_role`) and `Require(capability)`; implements `IOrganizationAccessGuard`. Legacy `AppUser.OrganizationId` fallback happens upstream in `MembershipService` read-repair, so the accessor stays claim-only.
- [x] 2.3 `Services/OrganizationAccess.cs`: `OrgCapability` enum, `OrgCapabilities.Allows` static table (Admin→all, Member→UseProjects+ViewEvidence), `ForbiddenException`, `IOrganizationAccessGuard`. `ForbiddenException` → 403 `{error:"forbidden"}` via one middleware in `Program.cs`.
- [x] 2.4 Admin-gated endpoints (`/dashboard/upgrade`, `/billing-portal`, token create/revoke) now call `currentOrg.Require(...)`. Member-accessible endpoints keep the existing `org_id is null → Forbid` guard, which already blocks non-members (the `org_id` claim is stamped only for members). Full per-endpoint capability conversion of the read/project-config routes deferred — no behaviour gap, only stylistic.
- [x] 2.5 `MembershipService.EnsureNotLastAdminAsync` — no-op unless the target is an admin, throws `ForbiddenException` if they are the last one. Wired into the member remove / role-change endpoints in Group 3.
- [x] 2.6 `OrganizationAccessGuardTests` (10): full capability matrix, `Require` permit/deny/no-org, last-admin protection, plus an HTTP test that a `Member` session gets 403 issuing a token while `Admin` gets 200. `TestClerkAuthHandler` + `CreateClientForOrg` gained a role override (defaults Admin, so existing tests are unchanged).
- [x] 2.7 `MembershipRole.Viewer` (design D9) added as value 0 (least-privileged `default`); `OrgCapabilities.Allows` third arm — `Viewer` → `ViewEvidence` only. `OrganizationAccessGuardTests` capability matrix extended (viewer denied `UseProjects`/`ManageTokens`/`ManageMembers`/`ManageBilling`/`ManageNotifications`, allowed `ViewEvidence`). `ChangeRoleAsync` now runs the last-admin check on any demotion (member **or** viewer), not just to member.

## 3. Provisioning + invites (spec: org-membership, account-provisioning)

- [x] 3.1 `ProvisioningService.GetOrCreateUserAsync` gains `pendingInviteToken`: new user + acceptable invite whose email matches (or no email claim) → created with `OrganizationId = Guid.Empty` (no throwaway org); else → org + founding `Admin` membership via the 3-item transaction. `X-Invite-Token` header forwarded from `OnTokenValidated`. Design D1a captured.
- [x] 3.2 `POST /api/organizations/{id}/invitations` — `Require(ManageMembers)` + route id must equal active org; bounded 14-day expiry (`OrganizationMembersService.InvitationLifetime`); response carries the accept URL
- [x] 3.3 `DELETE /api/organizations/{id}/invitations/{token}` — revoke (sets state `Revoked`)
- [x] 3.4 `GET /api/organizations/{id}/invitations` — list with state + accept URL
- [x] 3.5 `POST /api/invitations/{token}/accept` — validates acceptable, atomic `ClaimAsync` (invite marker + membership), idempotent if already a member, 409 `invitation-invalid` otherwise; runs the reconcile fallback
- [x] 3.6 `GET /api/organizations/{id}/members` (any member of that org) + `PATCH`/`DELETE .../members/{userId}` (`Require(ManageMembers)`), both routed through `EnsureNotLastAdminAsync`
- [x] 3.7 `POST /api/organizations` — creates org + founding `Admin` membership atomically (`CreateWithFounderAsync`); original org untouched
- [x] 3.8 `IInvitationEmailSender` + `LoggingInvitationEmailSender` (structured log). No transactional-email path exists in this service — a real SES sender is a flagged follow-up; until then the invite endpoint returns the accept URL for the admin to share. Link carries only the token.
- [x] 3.9 `OrganizationMembersServiceTests` (8): invite→accept→role, reconcile-away empty org, keep org-with-projects, expired/revoked reject, role fixed at issue, last-admin on change/remove, create-additional-org. `MembershipEndpointsHttpTests` (3): admin invite+list, member 403, wrong-org 403. `TestClerkAuthHandler` gained an orgless-session mode (`X-Test-Sub`).
- [x] 3.10 `viewer` is accepted as an invitable role by `POST .../invitations` and by `PATCH .../members/{userId}` (both already `Enum.TryParse<MembershipRole>` — "viewer" parses; no endpoint change). Tests: invite→accept as `viewer`, and demoting the last admin to `viewer` is refused.

## 4. Entitlement keys (spec: plan-tier-gating, design D5)

- [x] 4.1 `runNotifications` / `evidenceSharing` added to all three tiers in `hosted/plans.json` (Free false, Team/Enterprise true); `web/src/lib/plans.ts` (`EntitlementKey`, `ENTITLEMENT_KEYS`, `Entitlements`, `FEATURE_COPY`) + `web/src/lib/types.ts` `Entitlements` kept in sync — both shape-check validators (`EnsureComplete` C#, `validateCatalog` + `assertFeatureCopyComplete` TS) pass.
- [x] 4.2 Added to `PlanCatalog.EntitlementsDto` + `Entitlements` record → flow through `EntitlementService.For(org)` (incl. the `tier ∧ billing-status` degrade) and out on `DashboardView.entitlements` (the record is serialized whole — no DTO change needed).
- [x] 4.3 `EntitlementServiceTests`: Free denied both, Team + Enterprise granted both, a Canceled Team org degrades both to false. `web` build + eslint clean. Hosted suite 269 green.

## 5. Run notifications (spec: run-notifications, design D6)

- [x] 5.1 `NotificationTarget` entity (`PK=PROJECT#…`, `SK=NOTIFYTARGET#…`) + `NotificationTargetRepository` (list / get / put / delete / `RecordOutcomeAsync`).
- [x] 5.2 `NotificationEndpoints` — `/api/projects/{id}/notification-targets` GET/POST/PATCH(enabled)/DELETE. All `Require(ManageNotifications)` + project-in-org + `entitlements.For(org).RunNotifications` (403 `entitlement-required` `runNotifications`). `OutboundUrlValidator` on POST: https-only, rejects hosts resolving to loopback / RFC1918 / link-local / ULA / `169.254` / `0.0.0.0` / multicast — resolver injected (`Func<string,IPAddress[]>`) for offline tests.
- [x] 5.3 `hosted/terraform/notifications.tf` — SQS queue + DLQ (`maxReceiveCount` 5), `hosted_api` IAM `sqs:SendMessage`, `notification_dispatcher` Lambda (shared artifact, `RELEASETWIN_LAMBDA_TASK=NotificationDispatch`) + its role (SQS consume, DynamoDB Get/Query/Put) + `aws_lambda_event_source_mapping` with `ReportBatchItemFailures`. `Notifications__QueueUrl` + `Web__BaseUrl` added to the API function env. `terraform validate` clean.
- [x] 5.4 Ingest: after persist + usage increment, a failed case-report (`!Passed`) or a flag proof whose `Outcome != "Passed"` enqueues a `RunNotification` via `INotificationQueue`. `TryEnqueueAsync` gates on the `run-notifications` flag and swallows every error — ingest never blocks or fails.
- [x] 5.5 `NotificationDispatchService.DispatchAsync` — flag re-check, org entitlement re-check, enabled targets only, send-time `OutboundUrlValidator` re-check, payload = `{project, caseId, result, classification, reportId, reportKind, url}` (Slack gets `{text}`) with a `{webBase}/dashboard?projectId=…` link and no fixture/body/secret content. `HttpClient` named `notifications`: 5s timeout, `AllowAutoRedirect = false`. Program.cs `NotificationDispatch` branch parses a minimal SQS batch shape (no `Amazon.Lambda.SQSEvents` dep).
- [x] 5.6 `redrive_policy` → DLQ; the Lambda returns `batchItemFailures` so message-level failures (bad JSON, org load error) are retried by SQS → DLQ, while per-target HTTP failures are recorded (`RecordOutcomeAsync`) not thrown, so a retry never double-notifies a target that already succeeded.
- [x] 5.7 `flags.json` gains `run-notifications` (boolean, **default false**, hosted); `FLAG_KEYS` in `web/src/lib/flags-registry.ts` synced. Both the enqueue and the dispatch sides check it.
- [x] 5.8 Tests (+30, suite 307): `OutboundUrlValidatorTests` (7), `NotificationDispatchServiceTests` (7 — deliver+record, disabled skip, flag off, not-entitled, non-2xx, send-time SSRF re-check, Slack text), `NotificationEndpointsTests` (4 — CRUD, non-https/private rejected, member 403, Free entitlement 403), `NotificationTargetRepositoryTests` (1), `IngestNotificationEnqueueTests` (4 — failed enqueues, passing silent, flag-off silent, ineligible flag-proof). `CustomWebApplicationFactory` gained `ExtraConfiguration`, `NotificationQueueForTesting`, and an offline host resolver.

## 6. Evidence share links (spec: evidence-sharing, design D7)

- [x] 6.1 `ShareLink` entity + `ShareLinkRepository` — `PK=RUN#<reportId>`, `SK=SHARE#<tokenHash>`. Token = `<reportId>.<32 random bytes base64url>`, stored only as `ITokenService.Hash` (SHA-256). Report metadata a viewer may see (`caseId`/`result`/`classification`/`fixtureSha256`/`reportKind`) is denormalised onto the item at creation — resolving a link never reaches project/org-scoped data.
- [x] 6.2 `ShareLinkEndpoints` — `/api/reports/{reportId}/share-links` POST/GET/DELETE (`?projectId=`). New `OrgCapability.ManageSharing` (Admin-only via the existing table) + project-in-org + `evidenceSharing` entitlement (403 `entitlement-required`). POST returns `{id, token, url: {webBase}/share/{token}, expiresAt}`; 14-day default lifetime.
- [x] 6.3 `SharedEvidenceView` record (flat: caseId, reportKind, result, classification, fixtureSha256, hasEvidenceDocument, evidenceUploadedAt, document, screenshotIds). `EvidenceSharingViewShapeTests` — reflection asserts every property is in the whitelist, none is a `Guid`, and no name contains org/project/tenant/url.
- [x] 6.4 `GET /api/shared-runs/{token}` (`.AllowAnonymous`) — flag check → parse reportId → hash lookup → state/expiry → org `evidenceSharing` re-check (`ShareEntitlementRevokedException` → **403, not deleted**) → `SharedEvidenceView`. No evidence doc → `hasEvidenceDocument:false`, metadata-only. `+ /screenshots/{id}` anonymous proxy, validated against the link's evidence.
- [x] 6.5 `web/src/app/share/[token]/page.tsx` — outside `/dashboard`, not matched by the proxy's protected routes; server-fetches the hosted API's `/api/shared-runs/{token}` directly (no Clerk token); renders result + redacted legs + screenshots with no dashboard nav or links; `robots: noindex`. 404 → `notFound()`, 403 → "link no longer available". `screenshot/[id]/route.ts` anonymous proxy.
- [x] 6.6 `EvidencePurgeService` gains `IShareLinkRepository` (optional) — deletes every `SHARE#` item for a report whose evidence it purges.
- [x] 6.7 `evidence-sharing` flag in `flags.json` (boolean, **default false**, hosted) + `FLAG_KEYS`. Checked at resolve time; independent of the per-org entitlement.
- [x] 6.8 Tests (+17, suite 324): `EvidenceSharingViewShapeTests` (1), `EvidenceSharingServiceTests` (9 — create→resolve, metadata-only, revoke, expiry, flag off, downgrade-not-deleted-then-restored, token-does-not-generalize, purge, unknown report), `ShareLinkEndpointsTests` (4 — full lifecycle + anon resolve, member 403, Free 403, downgrade 403-without-delete), guard matrix extended for `ManageSharing`.

## 7. Onboarding activation (spec: onboarding-activation, design D8)

- [x] 7.1 `Organization.HasIngestedRealRun` (legacy rows read false). `IOrganizationRepository.MarkIngestedRealRunAsync` — read-check-write, a no-op once set. Called from both ingest handlers after persist.
- [x] 7.2 `Services/SampleProject.cs` — fixed well-known ids, `Name`, 2 canned case reports (1 pass "ORD-CHECKOUT-1", 1 fail "ORD-REFUND-7"), 1 flag-proof result, and a canned evidence drill-down (JSON envelope matching the real endpoint) for the failing case. Never persisted.
- [x] 7.3 `DashboardService`: when `!HasIngestedRealRun`, `SampleProject.Summary` (`IsExample: true`, `ReadOnly: true`) is appended to `Projects`; selecting it (or the default landing with no real project) returns its canned run history. Gone the moment `HasIngestedRealRun` flips. `DashboardEndpoints` evidence route serves `SampleProject.EvidenceFor` for a sample report id.
- [x] 7.4 Automatic — the sample id is never a real `Project`, so every `projects.GetAsync` / `ExistsInOrganizationAsync` ownership check fails closed (token issue → 403, delete/connection/secrets/creds → 403, ingest can't target it: no token can be issued). Tested explicitly for token issuance.
- [x] 7.5 Automatic — `_projects.ListByOrganizationAsync` never returns the sample, so `CreateProjectAsync`'s count check never counts it. Tested: a Free org showing the sample still creates its 1st real project, and the 2nd is rejected.
- [x] 7.6 `GuidedSetupView(HasProject, HasToken, ApiUrl, CliCommand)` on `DashboardView.GuidedSetup` (null after activation). `HasProject`/`HasToken` reflect real state; `CliCommand` is the `docker run` line with `RELEASETWIN_API_URL` (from `Api:PublicUrl` config, placeholder if unset) and a `<YOUR_TOKEN>` placeholder. `terraform` `api_public_url` var added (two-pass, like the GitHub OAuth vars).
- [x] 7.7 `web/src/app/dashboard/page.tsx` — `GuidedSetupPanel` (ordered steps with done-state + copyable command) rendered when `view.guidedSetup`; "Example" badge on `project.isExample`; for the sample selection the SetupSection / evidence-config / Journeys / tokens / ReleasesSection blocks are skipped (only run history + flag-proof tables + a note render), and the per-project fetches that would 403 are guarded.
- [x] 7.8 Tests (+8, suite 332): `SampleProjectServiceTests` (6 — shown+panel, panel reflects progress, retired after ingest incl. direct-select, `MarkIngested` idempotent, quota not consumed), `SampleProjectHttpTests` (3 — token issue 403, canned evidence drill-down + non-canned 403, first real ingest clears the sample + guided panel from the payload). web build + eslint clean.

## 8. Web UI (spec: org-membership, all)

- [ ] 8.1 Members & invitations settings page: list members + roles, invite form, revoke invite, change role, remove member (admin-only, hidden for members)
- [ ] 8.2 Accept-invite page at `/invitations/[token]` — works for signed-in and fresh signup
- [ ] 8.3 Active-org switcher in the app header; always shows the current org
- [ ] 8.4 Notification-targets settings UI per project with last-delivery status
- [ ] 8.5 Share-link controls on the run/evidence view: create, copy, list, revoke (Team-gated, shows upgrade prompt on Free)
- [ ] 8.6 Gate Team-only UI affordances on `DashboardView.entitlements`

## 9. Docs

- [ ] 9.1 Update `docs/customer-pilot-guide.md`: teams exist, paid signup path, what a share link is
- [ ] 9.2 Update `docs/installation-model.md` / README hosted-platform description for membership + notifications + sharing
- [ ] 9.3 Note in the change: Phase 1 per-project billing quantity = projects only; member count is not a billing axis

## 10. Verification

- [ ] 10.1 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green; report the new hosted test count
- [ ] 10.2 `cd web && npm run build` + `npx eslint` clean
- [ ] 10.3 `openspec validate commercial-readiness-gaps --strict`
- [ ] 10.4 Confirm CI (`ci.yml` / `hosted-ci.yml` / `web-ci.yml`) passes on the branch
- [ ] 10.5 **Needs the user to run this:** billing sandbox e2e (checkout → webhook → entitlement flip) per `docs/billing-sandbox-runbook.md` — hard prerequisite for charging money, tracked in the `billing-integration` change, not unblocked by code here
- [ ] 10.6 **Needs the user to run this:** `terraform apply` for the new GSI + SQS + DLQ (CI-only via OIDC — the plan runs in GitHub Actions, but confirm the applied output matches)
