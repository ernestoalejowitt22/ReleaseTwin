import { addClerkCommands } from "@clerk/testing/cypress";

addClerkCommands({ Cypress, cy });

// dashboard-visual-refresh: the dashboard's "Set up" section (Connection, Adapter credentials,
// Project secrets) collapses to a one-line summary once anything is configured — real, correct
// product behavior (see setup-section.tsx), but any spec that reloads after configuring something
// and then needs to interact with those cards again has to expand it first. Idempotent: does
// nothing if already expanded.
Cypress.Commands.add("expandSetupSection", () => {
  cy.get('[data-testid="setup-toggle"]').then(($toggle) => {
    if ($toggle.attr("data-state") === "closed") {
      cy.wrap($toggle).click();
    }
  });
});

// billing-integration: read the current org id off the dashboard and POST a signed Polar
// subscription event to the local hosted API's webhook — the only writer of billing-driven
// tier/status (design.md D2). Must be called while a `/dashboard` page is loaded.
Cypress.Commands.add(
  "sendBillingEvent",
  (type: string, opts: { status?: string; cadence?: string } = {}) => {
    cy.get("main[data-org-id]")
      .invoke("attr", "data-org-id")
      .then((orgId) => {
        if (!orgId) {
          throw new Error("dashboard did not render a data-org-id attribute");
        }
        return cy.task("sendBillingWebhook", {
          orgId,
          type,
          subscriptionId: `sub_${orgId}`,
          ...opts,
        });
      });
  },
);

// Convenience: put the current org on the Team tier via a `subscription.active` event, then
// reload and confirm. Replaces the old payment-free "click Upgrade" that several specs used to
// reach the paid tier before exercising evidence / secrets / journeys.
Cypress.Commands.add("elevateToTeam", () => {
  cy.sendBillingEvent("subscription.active");
  cy.reload();
  cy.contains("Team plan", { timeout: 10000 }).should("be.visible");
});

declare global {
  // eslint-disable-next-line @typescript-eslint/no-namespace
  namespace Cypress {
    interface Chainable {
      expandSetupSection(): Chainable<void>;
      sendBillingEvent(
        type: string,
        opts?: { status?: string; cadence?: string },
      ): Chainable<unknown>;
      elevateToTeam(): Chainable<void>;
    }
  }
}
