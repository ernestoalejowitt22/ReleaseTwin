# Billing sandbox runbook (Polar)

A click-by-click walkthrough to validate the `billing-integration` flow against Polar's **sandbox**
before touching production. Companion to `docs/billing.md` (which explains the model) and
`openspec/changes/billing-integration/design.md` (Migration Plan).

Everything here happens in Polar's sandbox environment (`https://sandbox.polar.sh`) — a separate
login and org from production, no bank details, no business verification. When it's green, you
repeat the config half against a real production Polar org and swap `POLAR_API_BASE_URL` back to
`https://api.polar.sh`.

---

## 0. What "done" looks like

- [ ] A test org checks out on Polar sandbox → its ReleaseTwin tier flips to **Team** within
      seconds, driven only by the webhook.
- [ ] Adding a project on that org bumps the Polar subscription quantity; a project delete lowers it.
- [ ] Cancelling in Polar's portal → the org keeps Team entitlements through the 14-day grace
      window, then degrades to Free; excess projects go read-only but keep their evidence.
- [ ] The nightly reconciliation Lambda logs (dry-run) with no drift on a clean org.

---

## 1. Polar sandbox: org + two products

At `https://sandbox.polar.sh`:

1. **Create an organization** (any name, e.g. `releasetwin-sandbox`).
2. **Products** → you should already have created:
   - `ReleaseTwin Team` — recurring, **every 1 month**, **seat-based** price **$59.00 USD / seat**.
   - `ReleaseTwin Team (annual)` — recurring, **every 1 year**, **seat-based** price **$588.00 USD
     / seat** (= $49 × 12).
   - Free trial: **off** on both. Customer-portal visibility: **Private**.
3. **Per-project = quantity.** Our code PATCHes `quantity` on the subscription as projects are
   added/removed and expects the invoice to be `unit price × quantity`. Set each product's price
   to a **seat-based** price (Polar: "Pricing based on number of seats"). If the sandbox test in
   §6 shows the quantity isn't multiplying the charge, the price type is wrong — fix it in Polar,
   no code change.
4. **Copy the two product ids.** Open each product → its id (the checkout API takes **product**
   ids, not price ids — `product_price_id` is deprecated). Note them as
   `POLAR_TEAM_PRODUCT_MONTHLY` / `POLAR_TEAM_PRODUCT_ANNUAL`.

## 2. Polar sandbox: API token + webhook

1. **Settings → Developers / API tokens** → create an **Organization Access Token**. Scopes:
   `checkouts:write`, `customer_sessions:write`, `subscriptions:read`, `subscriptions:write`.
   Copy it → `POLAR_API_TOKEN`. (Sandbox tokens are separate from production — a prod token won't
   work in the sandbox.)
2. **Settings → Webhooks → Add endpoint.**
   - URL: `https://aeq4mvkh3n63sqnngc4lp7567y0mqfzr.lambda-url.us-east-1.on.aws/api/billing/webhook`
     (the deployed hosted API's Function URL — stable across redeploys; confirm with
     `gh run view <latest deploy-hosted run> --log | grep function_url` if in doubt).
   - Format: **Raw** (Standard Webhooks — our `BillingWebhookSignature` verifies the
     `webhook-id` / `webhook-timestamp` / `webhook-signature` headers).
   - Events: `subscription.created`, `subscription.active`, `subscription.updated`,
     `subscription.canceled`, `subscription.revoked`, `subscription.uncanceled`,
     `subscription.past_due`, `order.paid`. (Extra events are harmless — unmapped types are
     recorded and no-op'd. `order.refunded` is optional and only for logging — refunds don't
     change entitlements; a refund that ends the subscription fires `subscription.revoked`, which
     is handled.)
   - Save, then open the endpoint and **copy the signing secret** → `POLAR_WEBHOOK_SECRET`.

   Until PR #41 is merged and deployed the URL returns 404; after that, 503 until the config below
   is set; then 200.

## 3. Set the config (repo secrets + variables)

GitHub → repo → **Settings → Secrets and variables → Actions**.

**Secrets:**

| Name | Value |
|---|---|
| `POLAR_API_TOKEN` | sandbox org access token |
| `POLAR_WEBHOOK_SECRET` | sandbox webhook signing secret |

**Variables:**

| Name | Value |
|---|---|
| `POLAR_API_BASE_URL` | `https://sandbox-api.polar.sh` |
| `POLAR_TEAM_PRODUCT_MONTHLY` | monthly product id (§1.4) |
| `POLAR_TEAM_PRODUCT_ANNUAL` | annual product id (§1.4) |
| `POLAR_CHECKOUT_SUCCESS_URL` | `https://releasetwin.com/dashboard?upgraded=1` |
| `POLAR_CHECKOUT_CANCEL_URL` | `https://releasetwin.com/dashboard` |
| `POLAR_PORTAL_RETURN_URL` | `https://releasetwin.com/dashboard` |
| `POLAR_RECONCILIATION_DRY_RUN` | `true` |
| `POLAR_UPGRADE_ENABLED` | `false` — leave the button hidden for now |

`https://releasetwin.com` is the `releasetwin` Vercel project's production URL. The
`?upgraded=1` param is currently cosmetic (the dashboard doesn't read it yet) — a bare
`/dashboard` is fine.

## 4. Deploy — webhook live, button hidden

Merge to `main` (or run **Deploy Hosted API** via `workflow_dispatch`). After it finishes:

- `PolarOptions.IsConfigured` is now true → `POST /api/billing/webhook` verifies signatures and
  processes events.
- `IsUpgradeEnabled` is still false → `/api/dashboard/upgrade` returns 503 and the dashboard shows
  no Upgrade button. Good — nobody can start a real checkout yet.
- Reconciliation Lambda runs nightly in dry-run.

**Smoke-test the webhook plumbing** from the Polar dashboard: Webhooks → your endpoint → **Send
test event** (`subscription.active`). Expect a `200`. A `401` means the secret is wrong; a `503`
means the config didn't land (check the deploy log and the Lambda env vars).

## 5. Drive a real sandbox checkout

The Upgrade button is hidden, so create the checkout by hand against the API:

1. Sign in to the deployed dashboard as a **test account** on the **Free** tier (one project).
   Note its org id (the dashboard URL / `/api/dashboard` response, or the DynamoDB `ORG#…` row).
2. Temporarily flip `POLAR_UPGRADE_ENABLED=true` and redeploy **OR** call the checkout endpoint
   directly with that account's Clerk session cookie:
   ```bash
   curl -X POST "<dashboard-url>/api/dashboard/upgrade" \
     -H "content-type: application/json" -d '{"cadence":"Monthly"}' \
     --cookie "<your browser's __session cookie>"
   ```
   (The dashboard BFF proxies to the API. Easiest is to flip the flag, use the button, then flip
   it back.)
3. Follow the returned `checkoutUrl`. Pay with a **Polar sandbox test card**
   (`4242 4242 4242 4242`, any future expiry/CVC).
4. Within a few seconds:
   - Polar delivers `subscription.active` (and/or `order.paid`) to the webhook.
   - `BillingEventProcessor` sets the org to **Team**, `BillingStatus=Active`, stores the Polar
     customer + subscription id + cadence.
   - The dashboard now shows "Team plan · Renews monthly" and a **Manage billing** link (once
     `POLAR_UPGRADE_ENABLED=true`).
   - **Redirect independence:** close the checkout tab before it redirects back — the org should
     still be Team, because only the webhook writes the tier.

## 6. Quantity sync

On the now-paying test org:

1. Create a second project → the Polar subscription quantity goes to **2** *before* the project is
   created. Check the subscription in the Polar dashboard.
2. Create a third → quantity **3**.
3. Delete one → quantity **2** (best-effort; the delete succeeds even if Polar is slow).
4. In the Polar dashboard, **manually set the quantity wrong** (e.g. to 1). Invoke the
   reconciliation Lambda once (`aws lambda invoke --function-name
   <prefix>releasetwin-billing-reconciliation /dev/stdout`) — with dry-run on it should **log**
   `billing_reconciliation_correction … polar_quantity=1 actual=2 dry_run=True` and change
   nothing. Flip `POLAR_RECONCILIATION_DRY_RUN=false`, redeploy, invoke again → it sets Polar back
   to 2.

## 7. Cancellation → grace → read-only

1. From the dashboard, **Manage billing** → cancel in Polar's portal (or cancel from the Polar
   dashboard).
2. Polar delivers `subscription.canceled` → `BillingStatus=Canceled`. Because our grace logic
   treats `Canceled` as an immediate drop, entitlements fall to **Free** right away. (For
   `past_due`, they'd hold for 14 days from the event timestamp — you can simulate that with a
   `subscription.updated` test event carrying `status: past_due` and an old `modified_at`.)
3. The test org had 3 projects; Free allows 1. Confirm:
   - The **oldest** project stays writable; the other two show a **Read-only** badge and a banner.
   - Uploading a case report with a read-only project's token → `403` with
     `{"error":"entitlement-required","entitlement":"maxProjects"}`.
   - All three projects still list with their existing evidence intact.
4. Re-subscribe (new checkout) → `subscription.active` → back to Team → all projects writable
   again.

## 8. Flip the button on

Once 5–7 are all green:

1. Set `POLAR_UPGRADE_ENABLED=true` (variable) and redeploy.
2. The dashboard Upgrade button (with the monthly/annual cadence picker) and Manage-billing link
   are now live for real customers on the sandbox deployment.
3. After one clean nightly reconciliation cycle with no spurious corrections, set
   `POLAR_RECONCILIATION_DRY_RUN=false`.

## 9. Production cutover

There's a single hosted deployment (no separate prod stack), so cutover is a config swap — but
first **clear the sandbox test orgs**, or they'll sit on Team with a `PolarSubscriptionId` that
only exists in Polar's sandbox (the nightly reconciliation Lambda will then log
`billing_reconciliation_read_failed` against the prod API for each, harmlessly, and they keep
entitlements they never paid for). For each test org: delete the test Clerk account / its `ORG#`
row, or `PUT /api/admin/organizations/{id}/tier` back to `Free`.

Then repeat sections 1–2 against a **production** Polar org and update the config:

- `POLAR_API_BASE_URL` → `https://api.polar.sh` (or delete the variable — it defaults to prod).
- New production `POLAR_API_TOKEN` / `POLAR_WEBHOOK_SECRET` / `POLAR_TEAM_PRODUCT_MONTHLY` /
  `POLAR_TEAM_PRODUCT_ANNUAL`.
- Register the production webhook at the same Function URL.
- Start with `POLAR_UPGRADE_ENABLED=false`, send a Polar test event, do one real checkout with a
  live card, then flip the button on.

Environment-specific config (GitHub Environments with separate `sandbox` / `production` values +
`table_prefix`) is a later change — only worth it once there's a staging environment you keep
running permanently.

**Rollback at any point:** set `POLAR_UPGRADE_ENABLED=false` (buttons vanish, webhook stays live
and idempotent). Orgs already on Team keep their tier.
