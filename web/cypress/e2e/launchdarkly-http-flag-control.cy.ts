import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * launchdarkly-http-flag-control: closes `flag-proof-control-readback` task 6.3 — the one open
 * item on the shipped `flag_proof.control` / `control.verify` read-back feature was proving it
 * works end-to-end against a real feature-flag REST API, not just a stubbed HttpMessageHandler.
 *
 * This is deliberately NOT the LaunchDarkly *adapter* path (`ld.readFeatureFlag` +
 * `LaunchDarklyFeatureStateController`) — that is already covered by
 * launchdarkly-real-flag-proof.cy.ts. Here the always-present HTTP adapter is the only thing
 * involved: the `control` block toggles a real LD flag with a JSON Patch `PATCH` to
 * `/api/v2/flags/...`, and `control.verify` reads it back with a `GET` + JSONPath on
 * `environments.<env>.on`.
 *
 * The LD REST token / project key live as hosted **project secrets** (entered through the real
 * dashboard form), so `${LD_API_TOKEN}` / `${LD_PROJECT_KEY}` in the generated case resolve via
 * CliRunner's hosted project-secrets fetch — the same resolver the `control` block shares with
 * `http.request` — not a local environment variable.
 *
 * The case's own pipeline reads the same flag back and asserts it is `on`, so the known-bad leg
 * (flag driven off) fails and the known-good leg (flag driven on) passes → a deterministic
 * `FLAGPROOF <id> (Passed)` regardless of the flag's value before this run.
 */
describe("launchdarkly http flag control", () => {
  const FLAG_KEY = "e2e.http-flag-control";
  const CASE_ID = `LD-HTTP-FLAGCTL-${Date.now()}`;

  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("runs a real flag-proof case that toggles + reads back a real LaunchDarkly flag over its REST API", () => {
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

      cy.get("body").then(($body) => {
        if ($body.text().includes("Free plan")) {
          cy.elevateToTeam();
        }
      });

      const projectName = `e2e-ld-http-${Date.now()}`;
      // Scoped by placeholder, not just `[name="name"]` — a selected project's own
      // "Project secrets" add-secret form also has an `input[name="name"]` on this same page.
      cy.get('input[placeholder="New project name"]').type(projectName);
      cy.contains("button", "Create project").click();
      cy.contains(projectName).should("be.visible");

      // The LD REST credentials go in as project secrets (not adapter credentials) — the HTTP
      // `control`/`verify` path resolves `${VAR}` through the same fetch `http.request` uses.
      const addSecret = (name: string, value: string) => {
        cy.contains(`Project secrets — ${projectName}`)
          .parents(".rounded-xl")
          .within(() => {
            cy.get('input[name="name"]').clear().type(name);
            cy.get('input[name="value"]').clear().type(value, { log: false });
            cy.contains("button", "Add secret").click();
          });
        cy.contains("Configured by").should("be.visible");
      };
      addSecret("LD_API_TOKEN", apiToken);
      addSecret("LD_PROJECT_KEY", projectKey);
      addSecret("LD_ENV_KEY", environmentKey);

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

      cy.task("writeHttpFlagControlCase", {
        directory: `/tmp/releasetwin-e2e-ld-http-${Date.now()}`,
        caseId: CASE_ID,
        flagKey: FLAG_KEY,
        environmentKey,
      }).then((writeResult) => {
        const { casesDir } = writeResult as { casesDir: string };

        cy.get("@apiToken").then((projectToken) => {
          cy.task(
            "runCli",
            {
              token: projectToken,
              apiUrl: (Cypress.env("RELEASETWIN_API_URL") as string | undefined) ?? "http://localhost:5199",
              casesDir,
              // Deliberately no LD_* here — resolution must come from the hosted project-secrets
              // fetch, or this test proves nothing about the shared `${VAR}` resolver.
            },
            { timeout: 180000 },
          ).then((runResult) => {
            const { stdout, stderr } = runResult as { code: number; stdout: string; stderr: string };
            expect(stdout, `stdout:\n${stdout}\nstderr:\n${stderr}`).to.match(
              new RegExp(`^FLAGPROOF ${CASE_ID} \\(Passed\\)$`, "m"),
            );
          });
        });
      });
    });
  });
});
