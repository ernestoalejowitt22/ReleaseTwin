import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * hosted-journeys 5.9: real end-to-end verification of the visual builder — sign in, create a
 * project, compose a login-then-call journey (Phase 1/2's capture/auth shape: capture a token from
 * one HTTP step, use it as a Bearer header in a later step) entirely through the builder UI, save
 * it, then run the saved version through the CLI's hosted-fetch path (`--journey <id>@<version>`)
 * and confirm it actually passes.
 */
describe("journey builder", () => {
  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("builds, saves, and runs a login-then-call journey end to end", () => {
    setupClerkTestingToken();
    cy.visit("/");
    cy.clerkLoaded();
    cy.clerkSignIn({
      strategy: "email_code",
      identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
    });
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");

    const projectName = `e2e-journey-${Date.now()}`;
    cy.get('input[name="name"]').type(projectName);
    cy.contains("button", "Create project").click();
    cy.contains(projectName).should("be.visible");

    cy.contains("button", "Issue new token").click();
    cy.contains("New token (shown once, copy it now):")
      .parent()
      .find("code")
      .first()
      .invoke("text")
      .then((token) => cy.wrap(token.trim()).as("apiToken"));

    cy.contains(`Journeys — ${projectName}`).parents(".rounded-xl").contains("Open builder").click();

    cy.contains("h1", "Journeys").should("be.visible");
    const journeyName = `e2e-login-then-call-${Date.now()}`;
    cy.get('input[name="name"]').type(journeyName);
    cy.contains("button", "Create journey").click();

    cy.contains("h1", journeyName).should("be.visible");
    cy.screenshot("journey-builder/01-blank-builder");

    const caseId = `E2E-LOGIN-THEN-CALL-${Date.now()}`;
    cy.get('input[placeholder="MY-JOURNEY-1"]').type(caseId);
    cy.get('input[placeholder="tickets/MY-JOURNEY-1"]').type(`t/${caseId}`);
    cy.get('input[placeholder="example.json"]').type("example-auth-chain.json");

    // Step 1: capture a token from a login-shaped endpoint.
    cy.contains("button", "Add step").click();
    cy.get('[data-testid="step-0"]').within(() => {
      cy.get('[data-testid="step-operation"]').type("http.request");
      cy.get('[data-testid="params"]').contains("button", "Add parameters").click();
      cy.get('[data-testid="params"] [data-testid="kv-key"]').type("url");
      cy.get('[data-testid="params"] [data-testid="kv-value"]').type("https://httpbin.org/uuid");
      cy.get('[data-testid="captures"]').contains("button", "Add capture").click();
      cy.get('[data-testid="capture-name"]').type("token");
      cy.get('[data-testid="capture-from"]').type("json:$.uuid");
    });

    // Step 2: use the captured token as a Bearer header on a second HTTP call.
    cy.contains("button", "Add step").click();
    cy.get('[data-testid="step-1"]').within(() => {
      cy.get('[data-testid="step-operation"]').type("http.request");
      cy.get('[data-testid="params"]').contains("button", "Add parameters").click();
      cy.get('[data-testid="params"] [data-testid="kv-key"]').type("url");
      cy.get('[data-testid="params"] [data-testid="kv-value"]').type("https://httpbin.org/bearer");
      cy.get('[data-testid="headers"]').contains("button", "Add headers").click();
      cy.get('[data-testid="headers"] [data-testid="kv-key"]').type("Authorization");
      cy.get('[data-testid="headers"] [data-testid="kv-value"]').type("Bearer {{token}}", { parseSpecialCharSequences: false });
    });

    cy.screenshot("journey-builder/02-pipeline-composed");

    cy.get("textarea[readonly]").invoke("val").should("include", "json:$.uuid");
    cy.get("textarea[readonly]").invoke("val").should("include", "Bearer {{token}}");

    cy.contains("button", "Save as new version").click();
    cy.contains("Saved as version 1.", { timeout: 10000 }).should("be.visible");
    cy.contains("--journey").should("be.visible");
    cy.screenshot("journey-builder/03-saved");

    cy.reload();
    cy.contains("Version history").parents(".rounded-xl").contains("td", "1").should("be.visible");

    cy.location("pathname").then((pathname) => {
      const journeyId = pathname.split("/").pop()!;
      cy.get("@apiToken").then((token) => {
        cy.task(
          "runCliJourney",
          {
            token,
            apiUrl: "http://localhost:5199",
            journeyRef: `${journeyId}@1`,
          },
          { timeout: 180000 },
        ).then((result: unknown) => {
          const { code, stdout, stderr } = result as { code: number; stdout: string; stderr: string };
          expect(stdout, `stdout:\n${stdout}\nstderr:\n${stderr}`).to.match(new RegExp(`^PASS ${caseId}$`, "m"));
          expect(code, `stdout:\n${stdout}\nstderr:\n${stderr}`).to.eq(0);
        });
      });
    });
  });
});
