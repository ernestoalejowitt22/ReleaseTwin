import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * billing-integration: end-to-end proof that a Polar subscription lifecycle drives the dashboard —
 * activation → Team, cancellation → Free entitlements with excess projects read-only (never deleted),
 * past-due → still Team inside the grace window, reactivation → restored.
 *
 * The webhook is the ONLY writer of billing-driven tier/status (design.md D2), so every state change
 * here is a signed `POST /api/billing/webhook` sent by `cy.sendBillingEvent` exactly as Polar's
 * servers would — the real Polar hosted checkout can't be driven from Cypress and isn't the point.
 * `e2e-api.mjs` starts the hosted API with the test Polar config that makes the webhook live.
 */
describe("billing lifecycle", () => {
  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("activation → Team, cancel → read-only, past-due grace, reactivation", () => {
    setupClerkTestingToken();
    cy.visit("/");
    cy.clerkLoaded();
    cy.clerkSignIn({
      strategy: "email_code",
      identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
    });
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");

    // --- activation → Team ---
    // A prior spec in the same run may already have this shared test org on Team; `subscription.active`
    // is idempotent ("set state Team + Active"), so this both sets up and asserts the active state.
    cy.sendBillingEvent("subscription.active", { cadence: "month" });
    cy.reload();
    cy.contains("Team plan").should("be.visible");
    cy.contains("Renews monthly").should("be.visible");
    cy.contains("button", "Manage billing").should("be.visible");
    cy.contains("button", "Upgrade").should("not.exist");
    cy.screenshot("billing/01-activated");

    // Team has no project cap — create three fresh projects for the read-only assertions below.
    const stamp = Date.now();
    const p1 = `e2e-billing-1-${stamp}`;
    const p2 = `e2e-billing-2-${stamp}`;
    const p3 = `e2e-billing-3-${stamp}`;
    for (const name of [p1, p2, p3]) {
      cy.get('input[placeholder="New project name"]').type(name);
      cy.contains("button", "Create project").click();
      cy.contains(name).should("be.visible");
    }

    // --- cancellation → Free entitlements, excess projects read-only ---
    cy.sendBillingEvent("subscription.canceled");
    cy.reload();
    cy.contains("Your subscription has been canceled").should("be.visible");
    // All three projects still listed with their evidence — none deleted.
    cy.contains(p1).should("be.visible");
    cy.contains(p2).should("be.visible");
    cy.contains(p3).should("be.visible");
    // Free's cap is 1, so the two newest are definitely read-only (which of the older projects keeps
    // the single writable slot depends on run order — the oldest-writable rule itself is covered by
    // ProjectWritabilityServiceTests).
    cy.get(`li:contains("${p2}")`).contains("Read-only").should("exist");
    cy.get(`li:contains("${p3}")`).contains("Read-only").should("exist");
    cy.screenshot("billing/02-canceled-read-only");

    // Free cap is back in force.
    cy.get('input[placeholder="New project name"]').type(`e2e-billing-blocked-${Date.now()}`);
    cy.contains("button", "Create project").click();
    cy.contains("Free plan is limited to 1 project").should("be.visible");

    // --- past-due inside the grace window → still Team ---
    cy.sendBillingEvent("subscription.updated", { status: "past_due" });
    cy.visit("/dashboard");
    cy.contains("Your last payment didn't go through").should("be.visible");
    // Grace window: full Team entitlements, so nothing is read-only.
    cy.get(`li:contains("${p2}")`).contains("Read-only").should("not.exist");
    cy.get(`li:contains("${p3}")`).contains("Read-only").should("not.exist");
    cy.screenshot("billing/03-past-due-grace");

    // --- reactivation → fully restored ---
    cy.sendBillingEvent("subscription.active");
    cy.visit("/dashboard");
    cy.contains("Team plan").should("be.visible");
    cy.contains("Your subscription has been canceled").should("not.exist");
    cy.contains("Your last payment didn't go through").should("not.exist");
    cy.contains("Read-only").should("not.exist");
    cy.screenshot("billing/04-reactivated");
  });
});
