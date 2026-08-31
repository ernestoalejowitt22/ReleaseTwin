# billing-integration — credential / config preflight

Per CLAUDE.md: everything the Polar (Merchant of Record) integration needs, with a one-line
verify per item. Standing manual steps (no code path) are flagged **MANUAL**.

> **Note (post-implementation):** the final shape differs from the early assumptions below —
> config is GitHub repo **secrets/variables** (not SSM), keys are `Polar__*`, and the checkout
> API takes **product** ids (not price ids). `docs/billing.md` + `docs/billing-sandbox-runbook.md`
> are the authoritative, current references. This file is kept for the credential-inventory intent.

## Polar account & catalog objects — **MANUAL** (Polar dashboard, no API to create these)

| Item | Purpose | Verify |
|---|---|---|
| Polar organization | the MoR account | `curl -sf -H "Authorization: Bearer $POLAR_API_TOKEN" https://api.polar.sh/v1/organizations/` |
| Product "ReleaseTwin Team" | the thing customers subscribe to | `curl -sf -H "Authorization: Bearer $POLAR_API_TOKEN" "https://api.polar.sh/v1/products/?organization_id=$POLAR_ORG_ID"` shows it |
| Product "ReleaseTwin Team" (monthly, seat-based, ~$59) | monthly cadence | product id → `POLAR_TEAM_PRODUCT_MONTHLY` |
| Product "ReleaseTwin Team (annual)" (yearly, seat-based, ~$49×12) | annual cadence | separate product; id → `POLAR_TEAM_PRODUCT_ANNUAL` |
| (Optional) Enterprise product + prices | only if Enterprise ever becomes self-serve — deferred, leave unset | n/a |
| Webhook endpoint registered → `https://<api-host>/api/billing/webhook` | delivers subscription events | Polar dashboard → Webhooks shows the endpoint "active"; secret copied to `POLAR_WEBHOOK_SECRET` |
| Customer portal enabled | "Manage billing" redirect target | Polar dashboard → Settings → Customer Portal is on |

## Secrets — GitHub Actions **repository secrets** (passed as Terraform `-var` by `deploy-hosted.yml`)

| Name | Bound to | Verify it's set |
|---|---|---|
| `POLAR_API_TOKEN` | `Polar__ApiToken` | `gh secret list \| grep POLAR_API_TOKEN` |
| `POLAR_WEBHOOK_SECRET` | `Polar__WebhookSecret` | `gh secret list \| grep POLAR_WEBHOOK_SECRET` |

## Non-secret config — GitHub repo **variables** (not secrets) + Terraform, like `CLERK_DOMAIN` / `ADMIN_OPERATOR_USER_IDS`

| Repo variable | Bound to | Verify |
|---|---|---|
| `POLAR_TEAM_PRODUCT_MONTHLY` | `Polar__ProductIds__Team__Monthly` | `gh variable list \| grep POLAR_TEAM_PRODUCT_MONTHLY` |
| `POLAR_TEAM_PRODUCT_ANNUAL` | `Polar__ProductIds__Team__Annual` | `gh variable list \| grep POLAR_TEAM_PRODUCT_ANNUAL` |
| `POLAR_API_BASE_URL` (optional; default `https://api.polar.sh`, sandbox `https://sandbox-api.polar.sh`) | `Polar__ApiBaseUrl` | `gh variable list \| grep POLAR_API_BASE_URL` |
| `POLAR_CHECKOUT_SUCCESS_URL` / `POLAR_CHECKOUT_CANCEL_URL` / `POLAR_PORTAL_RETURN_URL` | `Polar__CheckoutSuccessUrl` etc. | `gh variable list \| grep POLAR_` |
| `POLAR_RECONCILIATION_DRY_RUN` (default `true`) / `POLAR_UPGRADE_ENABLED` (default `false`) | `Polar__ReconciliationDryRun` / `Polar__UpgradeEnabled` | `gh variable list \| grep POLAR_` |

Empty / absent config ⇒ `PolarOptions.IsConfigured` is false: the app starts normally, the webhook
endpoint returns 503, and the upgrade/portal endpoints + dashboard button are hidden. The button
needs both `IsConfigured` **and** `POLAR_UPGRADE_ENABLED=true` (`IsUpgradeEnabled`), so the webhook
can be registered and events can flow before the button goes live.

## IAM — Lambda execution roles

| Principal | Permission | Why | Verify |
|---|---|---|---|
| API Lambda role | `ssm:GetParameter` on `/releasetwin/<env>/polar/*` | read Polar secrets at cold start | already granted for `/clerk/*`; extend the resource ARN list in Terraform |
| Reconciliation Lambda role | `dynamodb:Query`/`GetItem` on the table + GSIs; `ssm:GetParameter` on `/polar/*` | list orgs, read project counts, call Polar | new role, model on the EvidencePurge Lambda role |
| Reconciliation Lambda | outbound HTTPS to `api.polar.sh` | set subscription quantity | Lambda not in a locked-down VPC, or has a NAT route (EvidencePurge already reaches S3/AWS APIs; Polar is public internet — confirm egress) |

## GitHub Actions

- No new workflow scope. `secret-scan.yml` (`gitleaks`) already covers the repo; add `POLAR_` token
  patterns to `.gitleaks.toml` allowlist **only** for the `*.example`/docs placeholders, never real values.
- The deploy workflow (`deploy-hosted.yml`) passes the new repo variables through to Terraform as
  `TF_VAR_polar_*`; Terraform writes them into the Lambda environment. No `terraform apply` locally.

## Sandbox test (task 12.2) — **MANUAL**

1. Point `POLAR_API_BASE` at `https://sandbox-api.polar.sh`, use a sandbox token.
2. `Billing:Enabled=true` in a preview deploy.
3. Run a checkout with a Polar test card, confirm the webhook lands and the org moves to Team.
4. Add a project → confirm the sandbox subscription quantity goes to 2.
5. Cancel in the sandbox portal → confirm grace, then Free after the window.
