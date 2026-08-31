import crypto from "node:crypto";

// billing-integration e2e: the local hosted API (`e2e-api.mjs`) is started with these Polar values so
// `PolarOptions.IsConfigured` / `IsUpgradeEnabled` are true and the signed webhook is accepted. No
// real Polar call is ever made by the specs — the webhook path never calls Polar outbound, and the
// upgrade/portal endpoints are covered by the .NET suite, not Cypress.
export const E2E_POLAR_ENV = {
  Polar__ApiToken: "e2e-fake-token",
  Polar__WebhookSecret: "e2e-polar-webhook-secret",
  Polar__ProductIds__Team__Monthly: "e2e-product-team-monthly",
  Polar__ProductIds__Team__Annual: "e2e-product-team-annual",
  Polar__UpgradeEnabled: "true",
  // dry-run stays on: no spec drives the reconciliation job.
  Polar__ReconciliationDryRun: "true",
};

export const E2E_API_ORIGIN = "http://localhost:5199";

/**
 * Builds and POSTs a Standard-Webhooks-signed Polar subscription event to the local hosted API's
 * `/api/billing/webhook`, exactly as Polar would. Used by Cypress (`cy.task`) to move a test org
 * between billing states without touching Polar's hosted checkout.
 */
export async function sendBillingWebhook({
  orgId,
  type = "subscription.active",
  status,
  subscriptionId = "sub_e2e",
  customerId = "cus_e2e",
  cadence = "month",
  origin = E2E_API_ORIGIN,
}) {
  if (!orgId) {
    throw new Error("sendBillingWebhook: orgId is required");
  }

  const resolvedStatus =
    status ??
    (type.includes("canceled") || type.includes("revoked")
      ? "canceled"
      : type.includes("past_due")
        ? "past_due"
        : "active");

  const payload = {
    type,
    data: {
      id: subscriptionId,
      customer_id: customerId,
      status: resolvedStatus,
      recurring_interval: cadence,
      modified_at: new Date().toISOString(),
      metadata: { organization_id: orgId },
    },
  };
  const body = JSON.stringify(payload);
  const webhookId = `msg_${crypto.randomUUID()}`;
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const signature = crypto
    .createHmac("sha256", Buffer.from(E2E_POLAR_ENV.Polar__WebhookSecret, "utf8"))
    .update(`${webhookId}.${timestamp}.${body}`)
    .digest("base64");

  const response = await fetch(`${origin}/api/billing/webhook`, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      "webhook-id": webhookId,
      "webhook-timestamp": timestamp,
      "webhook-signature": `v1,${signature}`,
    },
    body,
  });

  const text = await response.text();
  if (!response.ok) {
    throw new Error(`billing webhook (${type}) returned ${response.status}: ${text}`);
  }
  return { status: response.status, body: text };
}
