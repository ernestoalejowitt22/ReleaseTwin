# Go-public runbook

Tracks the `go-public-sequence` change — making the hosted platform reachable by
outside users. Ordered; each section records the decision or the completion.

## 1. Prod-stack decision

**Decision (2026-09-01): pilot on the existing `releasetwin-dev-` stack. Cut a
dedicated prod stack only at customer #2.**

The auto-deploy stack (`table_prefix = "releasetwin-dev-"`, applied by
`deploy-hosted.yml`) already carries everything a production tenant needs:

| Concern | Where | Status |
|---|---|---|
| API 5xx / Lambda error / throttle alarms → SNS → operator email | `hosted/terraform/alerting.tf` | ✅ applied |
| Daily staleness digest (second Lambda) | `hosted/terraform/alerting.tf` | ✅ applied |
| Evidence blob bucket (S3, private, SSE) | `hosted/terraform/evidence.tf` | ✅ applied |
| Scheduled evidence purge (per-project retention) | `hosted/terraform/evidence.tf` | ✅ applied |
| Run-failure notification queue + dispatcher Lambda | `hosted/terraform/notifications.tf` | ✅ applied (feature-flagged off) |
| Billing reconciliation + metrics digest | `hosted/terraform/billing.tf` | ✅ applied |
| DynamoDB PITR / TTL | `hosted/terraform/main.tf` | TTL on; PITR is a one-line add when cutting prod |

The `-dev-` prefix is **cosmetic** — it is the only stack, nothing labelled
"dev" is visible to anyone hitting `releasetwin.com`, and local development uses
DynamoDB Local (not this stack), so there is no data bleed. Renaming it is a
one-time migration (§1.1) that is cheapest at low volume; deferring it costs
almost nothing.

**Cut a real prod stack when:** a second paying customer exists, or a
compliance ask requires named-prod isolation — whichever first.

### 1.1 Deferred DynamoDB-prefix migration (when prod is cut)

The single table is `${table_prefix}ReleaseTwinHosted` + the evidence bucket
`${table_prefix}releasetwin-evidence-blobs` + every `${table_prefix}releasetwin-*`
resource. To move to a `releasetwin-prod-` (or unprefixed) stack:

1. `workflow_dispatch` **Deploy Hosted API** with `table_prefix=releasetwin-prod-`
   (and `region`) — creates the parallel stack, empty.
2. Freeze writes briefly (maintenance window — trivial at pilot scale).
3. Copy the DynamoDB table: on-demand export to S3 → import into the new table,
   **or** a scripted `Scan` + `BatchWriteItem` for a small table.
4. Copy the evidence bucket: `aws s3 sync s3://releasetwin-dev-releasetwin-evidence-blobs
   s3://releasetwin-prod-releasetwin-evidence-blobs`.
5. Re-point config: `Api__PublicUrl` self-heals from the new function URL on the
   next apply; update `WEB_BASE_URL` only if the function URL is referenced
   directly anywhere (it is not — the web app calls the API via its own env).
6. Update `AWS_DEPLOY_ROLE_ARN` scope in `hosted/terraform-bootstrap` if the
   resource-name prefix in its IAM statements changes (`releasetwin-dev-*` →
   `releasetwin-prod-*`).
7. Soak the new stack; then `terraform destroy` the old one (or let it idle —
   PAY_PER_REQUEST + empty = ~$0).

Enable PITR on the prod table (`point_in_time_recovery { enabled = true }` in
`main.tf`) as part of this cut.

## 2. Repo history-cache expiry (prerequisite for any public flip)

- [ ] Email GitHub Support to expire cached pre-history-rewrite SHAs on
  `ReleaseTwin` and `NAHA` (history was `filter-repo` + force-pushed 2026-08-30).
- [ ] Get written confirmation; then a fresh clone +
  `git rev-list --all | xargs -I{} git grep -i <prior-vendor-term> {}` returns zero.

Blocked on: user action + written reply from GitHub.

## 3. Repo visibility

Blocked on §2 **and** `company-and-domain-launch` §6.7 (licensing legal review),
which is itself blocked on the LLC. Do not flip before both.

- [ ] Polish `ReleaseTwin` repo description + topics; `SECURITY.md` contact on the
  company domain (waits on Workspace — `company-and-domain-launch` group 2).
- [ ] Flip `ReleaseTwin` → public.
- [ ] `NAHA`: decision pending (open question) — it only needs to stay a working
  Vercel Preview as the demo target; a public repo is optional.

## 4. Self-serve sign-up

- [x] **Marketing CTAs wired to `/sign-up`** (2026-09-01): site header gains a
  primary "Sign up" button; homepage hero → "Get started free"; pricing Free
  tier and the hosted-platform doc → `/sign-up`.
- [ ] Full funnel walk on the live site (sign-up → org provisioned → first
  project → Free entitlements) — needs a real sign-up; do it once the operator
  account exists on the prod Clerk instance.
- [ ] `ADMIN_OPERATOR_USER_IDS` repo var — **not set**. Set it to the operator's
  Clerk **production** user id after signing up (Actions → Variables). Empty ⇒
  the Enterprise-tier admin endpoint + admin surface stay closed (safe default).
- [ ] Real external sign-up + upgrade-to-Team round trip — blocked on
  `company-and-domain-launch` §7 (Polar in production).

## 5. Announcement readiness

Deferred (marketing act): Show HN / Product Hunt / marketplace listings, `/blog`.
