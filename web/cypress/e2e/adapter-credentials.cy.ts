import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * hosted-adapter-credentials: real end-to-end verification of the dashboard mechanics — sign in,
 * create a project, set LaunchDarkly credentials through the dashboard form, confirm they persist
 * across a reload as "configured" metadata, and confirm the value is never redisplayed. The CLI's
 * hosted-fetch side of the round trip (task 7.1) is verified separately, directly against the
 * hosted API — a full round trip through real LaunchDarkly itself isn't achievable in this
 * environment (no real LaunchDarkly credentials), the same blocker chained-journeys' task 3.5 has.
 */
describe("adapter credentials", () => {
  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("sets LaunchDarkly credentials via the dashboard and they persist without ever being redisplayed", () => {
    setupClerkTestingToken();
    cy.visit("/");
    cy.clerkLoaded();
    cy.clerkSignIn({
      strategy: "email_code",
      identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
    });
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");

    const projectName = `e2e-adapter-cred-${Date.now()}`;
    cy.get('input[name="name"]').type(projectName);
    cy.contains("button", "Create project").click();
    cy.contains(projectName).should("be.visible");

    cy.contains(`Adapter credentials — ${projectName}`)
      .parents(".rounded-xl")
      .within(() => {
        cy.contains("LaunchDarkly")
          .parents('[class*="rounded-lg"]')
          .within(() => {
            cy.contains("Not configured").should("be.visible");
            cy.get('input[name="apiToken"]').type("fake-ld-api-token");
            cy.get('input[name="projectKey"]').type("e2e-project");
            cy.get('input[name="environmentKey"]').type("production");
            cy.contains("button", "Save").click();
          });
      });

    cy.reload();
    cy.screenshot("adapter-credentials/01-configured");

    cy.contains(`Adapter credentials — ${projectName}`)
      .parents(".rounded-xl")
      .within(() => {
        cy.contains("LaunchDarkly")
          .parents('[class*="rounded-lg"]')
          .within(() => {
            // Scenario: Setting credentials for a project — persisted metadata is now shown.
            cy.contains("Configured by").should("be.visible");
            // Scenario: Stored credential values are never redisplayed once set.
            cy.get('input[name="apiToken"]').should("have.value", "");
            cy.get('input[name="projectKey"]').should("have.value", "");
            cy.contains("fake-ld-api-token").should("not.exist");

            // Scenario: Rotating replaces the value entirely.
            cy.get('input[name="apiToken"]').type("fake-ld-api-token-rotated");
            cy.get('input[name="projectKey"]').type("e2e-project");
            cy.get('input[name="environmentKey"]').type("production");
            cy.contains("button", "Rotate").click();
          });
      });

    cy.reload();
    cy.contains(`Adapter credentials — ${projectName}`)
      .parents(".rounded-xl")
      .within(() => {
        cy.contains("LaunchDarkly")
          .parents('[class*="rounded-lg"]')
          .within(() => {
            // Scenario: Revoking removes the credential from future fetches.
            cy.contains("button", "Revoke").click();
          });
      });

    cy.reload();
    cy.contains(`Adapter credentials — ${projectName}`)
      .parents(".rounded-xl")
      .within(() => {
        cy.contains("LaunchDarkly").parents('[class*="rounded-lg"]').within(() => {
          cy.contains("Not configured").should("be.visible");
        });
      });
    cy.screenshot("adapter-credentials/02-revoked");
  });
});
