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

declare global {
  // eslint-disable-next-line @typescript-eslint/no-namespace
  namespace Cypress {
    interface Chainable {
      expandSetupSection(): Chainable<void>;
    }
  }
}
