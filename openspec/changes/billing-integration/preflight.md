# billing-integration — credential / config preflight

Per CLAUDE.md: everything the Polar (Merchant of Record) integration needs, with a one-line
verify per item. Standing manual steps (no code path) are flagged **MANUAL**.

## Polar account & catalog objects — **MANUAL** (Polar dashboard, no API to create these)

| Item | Purpose | Verify |
|---|---|---|
| Polar organization | the MoR account | `curl -sf -H "Authorization: Bearer $POLAR_API_TOKEN" https://api.polar.sh/v1/organizations/` |
| Product "ReleaseTwin Team" | the thing customers subscribe to | `curl -sf -H "Authorization: Bearer $POLAR_API_TOKEN" "https://api.polar.sh/v1/products/?organization_id=$POLAR_ORG_ID"` shows it |
| Price: Team monthly (recurring, monthly, ~$59, quantity-based / per-seat) | monthly cadence | product JSON `prices[]` has a `recurring_interval: month` entry; id → `POLAR_TEAM_PRICE_MONTHLY` |
| Price: Team annual (recurring, yearly, ~$49×12) | annual cadence | product JSON `prices[]` has a `recurring_interval: year` entry; id → `POLAR_TEAM_PRICE_ANNUAL` |
| (Optional) Enterprise product + prices | only if Enterprise ever becomes self-serve — deferred, leave unset | n/a |
| Webhook endpoint registered → `https://<api-host>/api/billing/webhook` | delivers subscription events | Polar dashboard → Webhooks shows the endpoint "active"; secret copied to `POLAR_WEBHOOK_SECRET` |
| Customer portal enabled | "Manage billing" redirect target | Polar dashboard → Settings → Customer Portal is on |

## Secrets — SSM Parameter Store (SecureString), same pattern as Clerk

| Name | Verify it's set |
|---|---|
| `/releasetwin/<env>/polar/api-token` → env `POLAR_API_TOKEN` | `aws ssm get-parameter --name /releasetwin/dev/polar/api-token --with-decryption --query Parameter.Value --output text` |
| `/releasetwin/<env>/polar/webhook-secret` → env `POLAR_WEBHOOK_SECRET` | `aws ssm get-parameter --name /releasetwin/dev/polar/webhook-secret --with-decryption --query Parameter.Value --output text` |

## Non-secret config — GitHub repo **variables** (not secrets) + Terraform, like `CLERK_DOMAIN` / `ADMIN_OPERATOR_USER_IDS`

| Repo variable | Bound to | Verify |
|---|---|---|
| `POLAR_ORGANIZATION_ID` | `Polar:OrganizationId` | `gh variable list \| grep POLAR_ORGANIZATION_ID` |
| `POLAR_TEAM_PRODUCT_ID` | `Polar:Team:ProductId` | `gh variable list \| grep POLAR_TEAM_PRODUCT_ID` |
| `POLAR_TEAM_PRICE_MONTHLY` | `Polar:Team:MonthlyPriceId` | `gh variable list \| grep POLAR_TEAM_PRICE_MONTHLY` |
| `POLAR_TEAM_PRICE_ANNUAL` | `Polar:Team:AnnualPriceId` | `gh variable list \| grep POLAR_TEAM_PRICE_ANNUAL` |
| `POLAR_API_BASE` (optional; default `https://api.polar.sh`, sandbox `https://sandbox-api.polar.sh`) | `Polar:ApiBase` | `gh variable list \| grep POLAR_API_BASE` |

Empty / absent config ⇒ `IPolarClient` construction fails fast at startup **only if** the upgrade
surface is enabled; with billing disabled (`Billing:Enabled=false`, the default until sandbox
passes) the app starts normally and the webhook endpoint is a signature-checked no-op.

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
