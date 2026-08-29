import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * launchdarkly-real-flag-proof: closes the real-verification gap flagged by both
 * chained-journeys' task 3.5 and hosted-adapter-credentials' task 7.1 — sets real LaunchDarkly
 * credentials through the real dashboard adapter-credentials form (sourced from AWS Secrets
 * Manager, see fetchLaunchDarklyTestAccount in cypress.config.ts), issues a real project token,
 * and runs the real CLI through the hosted-credential-fetch path against a real LaunchDarkly
 * flag. The flag key lives here, not in the secret, so future tests can target other flags
 * without touching Secrets Manager.
 *
 * The case's own pipeline (`ld.readFeatureFlag`) reads back the exact flag `flag_proof` just
 * toggled — a round-trip self-check of real toggle+read against LaunchDarkly, not a check of any
 * downstream application — so the outcome is deterministically "Passed" regardless of whatever
 * the flag's value was before this test ran.
 */
describe("launchdarkly real flag proof", () => {
  const FLAG_KEY = "naha.service-catalog-api";
  const CASE_ID = `LD-REAL-FLAGPROOF-${Date.now()}`;

  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("runs a real flag-proof case against a real LaunchDarkly flag via the hosted credential fetch", () => {
    cy.task("fetchLaunchDarklyTestAccount").then((account) => {
      const { apiToken, projectKey, environmentKey } = account as {
        apiToken: string;
        projectKey: string;
        environmentKey: string;
      };

      setupClerkTestingToken();
      cy.visit("/");
      cy.clerkLoaded();
      cy.clerkSignIn({
        strategy: "email_code",
        identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
      });
      cy.visit("/dashboard");
      cy.contains("h1", "Dashboard").should("be.visible");

      const projectName = `e2e-ld-real-${Date.now()}`;
      // Scoped by placeholder, not just `[name="name"]` — once a project is selected, its "Set up"
      // section's project-secrets add-secret form also has an `input[name="name"]` on this same page.
      cy.get('input[placeholder="New project name"]').type(projectName);
      cy.contains("button", "Create project").click();
      cy.contains(projectName).should("be.visible");

      // Real credentials, not the fake ones adapter-credentials.cy.ts uses — this is the one spec
      // that needs the round trip to actually reach LaunchDarkly.
      cy.contains(`Adapter credentials — ${projectName}`)
        .parents(".rounded-xl")
        .within(() => {
          cy.contains("LaunchDarkly")
            .parents('[class*="rounded-lg"]')
            .within(() => {
              cy.get('input[name="apiToken"]').type(apiToken, { log: false });
              cy.get('input[name="projectKey"]').type(projectKey);
              cy.get('input[name="environmentKey"]').type(environmentKey);
              cy.contains("button", "Save").click();
            });
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

      cy.task("writeLaunchDarklyFlagProofCase", {
        directory: `/tmp/releasetwin-e2e-ld-${Date.now()}`,
        caseId: CASE_ID,
        flagKey: FLAG_KEY,
      }).then((writeResult) => {
        const { casesDir } = writeResult as { casesDir: string };

        cy.get("@apiToken").then((projectToken) => {
          cy.task(
            "runCli",
            {
              token: projectToken,
              apiUrl: "http://localhost:5199",
              casesDir,
              env: { LAUNCHDARKLY_FLAG_KEY: FLAG_KEY },
            },
            { timeout: 180000 },
          ).then((runResult) => {
            const { stdout, stderr } = runResult as { code: number; stdout: string; stderr: string };
            // The CLI has no local LAUNCHDARKLY_* env vars set here — this forces the
            // hosted-adapter-credentials fetch path (CliRunner.cs), not the env-var path, so this
            // exercises the exact round trip a customer running the CLI from their own CI would.
            expect(stdout, `stdout:\n${stdout}\nstderr:\n${stderr}`).to.match(
              new RegExp(`^FLAGPROOF ${CASE_ID} \\(Passed\\)$`, "m"),
            );
          });
        });
      });
    });
  });
});
