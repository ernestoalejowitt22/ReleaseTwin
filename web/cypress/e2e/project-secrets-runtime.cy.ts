import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * hosted-project-secrets 7.2: closes the real-verification loop project-secrets.cy.ts's dashboard
 * mechanics alone don't cover — signs in, upgrades to Paid, creates a project, sets a real project
 * secret (a real, publicly reachable base URL) through the real dashboard form, issues a real
 * token, then runs the real CLI — via the hosted-fetch fallback, with no local environment variable
 * of that name set — against a case file that references `${VAR_NAME}` in its request URL. A real
 * PASS confirms the hosted secret actually resolved and drove a real HTTP call, not just that the
 * dashboard round trip looks right.
 */
describe("project secrets runtime", () => {
  const VAR_NAME = "E2E_SECRET_BASE_URL";
  const CASE_ID = `SECRET-RUNTIME-${Date.now()}`;

  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("resolves a ${VAR_NAME} reference from a hosted project secret when the local environment has none", () => {
    setupClerkTestingToken();
    cy.visit("/");
    cy.clerkLoaded();
    cy.clerkSignIn({
      strategy: "email_code",
      identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
    });
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");

    cy.get("body").then(($body) => {
      if ($body.text().includes("Free plan")) {
        cy.contains("button", "Upgrade").click();
        cy.contains("Paid plan").should("be.visible");
      }
    });

    const projectName = `e2e-secrets-runtime-${Date.now()}`;
    // Scoped by placeholder, not just `[name="name"]` — a previously-selected project's own
    // "Project secrets" add-secret form also has an `input[name="name"]` on this same page.
    cy.get('input[placeholder="New project name"]').type(projectName);
    cy.contains("button", "Create project").click();
    cy.contains(projectName).should("be.visible");

    cy.contains(`Project secrets — ${projectName}`)
      .parents(".rounded-xl")
      .within(() => {
        cy.get('input[name="name"]').type(VAR_NAME);
        cy.get('input[name="value"]').type("https://jsonplaceholder.typicode.com");
        cy.contains("button", "Add secret").click();
      });
    cy.contains("Configured by").should("be.visible");

    cy.contains("button", "Issue new token").click();
    cy.contains("New token (shown once, copy it now):").should("be.visible");
    cy.contains("New token (shown once, copy it now):")
      .parent()
      .find("code")
      .first()
      .invoke("text")
      .then((token) => {
        cy.wrap(token.trim()).as("apiToken");
      });

    cy.task("writeProjectSecretCase", {
      directory: `/tmp/releasetwin-e2e-secrets-${Date.now()}`,
      caseId: CASE_ID,
      varName: VAR_NAME,
    }).then((writeResult) => {
      const { casesDir } = writeResult as { casesDir: string };

      cy.get("@apiToken").then((projectToken) => {
        cy.task(
          "runCli",
          {
            token: projectToken,
            apiUrl: "http://localhost:5199",
            casesDir,
            // Deliberately no E2E_SECRET_BASE_URL here — resolution must come from the hosted
            // fetch, not a local environment variable, or this test proves nothing.
          },
          { timeout: 180000 },
        ).then((runResult) => {
          const { stdout, stderr } = runResult as { code: number; stdout: string; stderr: string };
          expect(stdout, `stdout:\n${stdout}\nstderr:\n${stderr}`).to.match(new RegExp(`^PASS ${CASE_ID}$`, "m"));
        });
      });
    });
  });
});
