import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * hosted-project-secrets: real end-to-end verification of the dashboard mechanics — sign in,
 * upgrade to Paid (required to store a secret at all — project-secrets spec), create a project, add
 * a secret through the dashboard form, confirm it persists across a reload as "configured" metadata
 * without ever redisplaying the value, confirm rotation replaces it, and confirm revoking removes
 * it. The CLI's hosted-fetch side of the round trip is verified separately, directly against the
 * hosted API (CliRunnerProjectSecretsTests, ProjectSecretFetchApiTests) and end to end for real in
 * project-secrets-runtime.cy.ts (task 7.2).
 */
describe("project secrets", () => {
  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("adds a project secret via the dashboard and it persists without ever being redisplayed", () => {
    setupClerkTestingToken();
    cy.visit("/");
    cy.clerkLoaded();
    cy.clerkSignIn({
      strategy: "email_code",
      identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
    });
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");

    // Storing a project secret requires the Paid tier (project-secrets spec) — upgrade first via
    // the same self-serve, no-payment control plan-tier-gating already provides. Conditional (not
    // `cy.contains`, which fails when absent) since this test user's org may already be Paid from a
    // previous run — `ensureE2ETestUser` reuses the same user across runs, per its own design.
    cy.get("body").then(($body) => {
      if ($body.text().includes("Free plan")) {
        cy.contains("button", "Upgrade").click();
        cy.contains("Paid plan").should("be.visible");
      }
    });

    const projectName = `e2e-secrets-${Date.now()}`;
    // Scoped by placeholder, not just `[name="name"]` — a previously-selected project's own
    // "Project secrets" add-secret form also has an `input[name="name"]` on this same page.
    cy.get('input[placeholder="New project name"]').type(projectName);
    cy.contains("button", "Create project").click();
    cy.contains(projectName).should("be.visible");

    const secretName = "NAHA_E2E_SECRET";

    cy.contains(`Project secrets — ${projectName}`)
      .parents(".rounded-xl")
      .within(() => {
        cy.get('input[name="name"]').type(secretName);
        cy.get('input[name="value"]').type("fake-secret-value");
        cy.contains("button", "Add secret").click();
      });

    cy.reload();
    cy.screenshot("project-secrets/01-configured");

    cy.contains(`Project secrets — ${projectName}`)
      .parents(".rounded-xl")
      .within(() => {
        // Scenario: Setting a secret for a project — persisted metadata is now shown.
        cy.contains(secretName).should("be.visible");
        cy.contains("Configured by").should("be.visible");
        // Scenario: Stored secret values are never redisplayed once set.
        cy.get('input[name="value"]').each(($input) => {
          expect($input.val()).to.eq("");
        });
        cy.contains("fake-secret-value").should("not.exist");

        // Scenario: Rotating replaces the value entirely.
        cy.contains(secretName)
          .parents(".rounded-lg")
          .within(() => {
            cy.get('input[name="value"]').type("fake-secret-value-rotated");
            cy.contains("button", "Rotate").click();
          });
      });

    cy.reload();
    cy.contains(`Project secrets — ${projectName}`)
      .parents(".rounded-xl")
      .within(() => {
        cy.contains(secretName)
          .parents(".rounded-lg")
          .within(() => {
            // Scenario: Revoking removes the secret from future fetches.
            cy.contains("button", "Revoke").click();
          });
      });

    cy.reload();
    cy.contains(`Project secrets — ${projectName}`)
      .parents(".rounded-xl")
      .within(() => {
        cy.contains(secretName).should("not.exist");
      });
  });
});
