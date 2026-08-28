import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * naha-real-journey: the real-target counterpart of journey-builder.cy.ts (which proves the builder
 * mechanics against generic echo endpoints). This one signs in via real Clerk auth, creates a real
 * project, stores NAHA's real e2e login secret through the real project-secrets dashboard form,
 * composes a real login-then-call journey against NAHA's actual deployed API entirely through the
 * builder UI, saves it, and runs the saved version through the CLI's hosted-fetch path — closing
 * the loop end to end: real dashboard, real hosted secret, real journey content, real third-party
 * API, real pass.
 *
 * Real NAHA credentials (e2e auth secret, API base URL, admin test-user email) come from AWS
 * Secrets Manager (see fetchNahaTestAccount in cypress.config.ts) — never hardcoded here.
 */
describe("naha real journey", () => {
  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("builds a real login-then-call journey against NAHA and runs it end to end", () => {
    cy.task("fetchNahaTestAccount").then((account) => {
      const { e2eAuthSecret, apiBaseUrl, adminEmail } = account as {
        e2eAuthSecret: string;
        apiBaseUrl: string;
        adminEmail: string;
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

      // Storing a project secret requires the Paid tier — upgrade first if this test user's org
      // is still Free (conditional: `ensureE2ETestUser` reuses the same user across runs, so a
      // previous run may have already upgraded it).
      cy.get("body").then(($body) => {
        if ($body.text().includes("Free plan")) {
          cy.contains("button", "Upgrade").click();
          cy.contains("Paid plan").should("be.visible");
        }
      });

      const projectName = `e2e-naha-journey-${Date.now()}`;
      cy.get('input[placeholder="New project name"]').type(projectName);
      cy.contains("button", "Create project").click();
      cy.contains(projectName).should("be.visible");

      cy.contains(`Project secrets — ${projectName}`)
        .parents(".rounded-xl")
        .within(() => {
          cy.get('input[name="name"]').type("NAHA_E2E_SECRET");
          cy.get('input[name="value"]').type(e2eAuthSecret, { log: false });
          cy.contains("button", "Add secret").click();
        });
      cy.contains("Configured by").should("be.visible");

      cy.contains("button", "Issue new token").click();
      cy.contains("New token (shown once, copy it now):")
        .parent()
        .find("code")
        .first()
        .invoke("text")
        .then((token) => cy.wrap(token.trim()).as("apiToken"));

      cy.contains(`Journeys — ${projectName}`).parents(".rounded-xl").contains("Open builder").click();
      cy.contains("h1", "Journeys").should("be.visible");

      const journeyName = `e2e-naha-journey-${Date.now()}`;
      cy.get('input[name="name"]').type(journeyName);
      cy.contains("button", "Create journey").click();
      cy.contains("h1", journeyName).should("be.visible");

      const caseId = `E2E-NAHA-JOURNEY-${Date.now()}`;
      cy.get('input[placeholder="MY-JOURNEY-1"]').type(caseId);
      cy.get('input[placeholder="tickets/MY-JOURNEY-1"]').type("naha.backend/docs/api-auth.md");
      cy.get('input[placeholder="example.json"]').type("e2e-naha-real-journey.json");

      // Step 1: real NAHA e2e login — the shared secret resolves from the project secret just set,
      // not a local env var, exactly like a real customer's CI would see it.
      cy.contains("button", "Add step").click();
      cy.get('[data-testid="step-0"]').within(() => {
        cy.get('[data-testid="step-operation"]').type("http.request");
        cy.get('[data-testid="params"]').contains("button", "Add parameters").click();
        cy.get('[data-testid="params"] [data-testid="kv-key"]').eq(0).type("url");
        cy.get('[data-testid="params"] [data-testid="kv-value"]').eq(0).type(`${apiBaseUrl}/v1/e2e/login`);
        cy.get('[data-testid="params"]').contains("button", "Add parameters").click();
        cy.get('[data-testid="params"] [data-testid="kv-key"]').eq(1).type("method");
        cy.get('[data-testid="params"] [data-testid="kv-value"]').eq(1).type("POST");
        cy.get('[data-testid="params"]').contains("button", "Add parameters").click();
        cy.get('[data-testid="params"] [data-testid="kv-key"]').eq(2).type("body");
        cy.get('[data-testid="params"] [data-testid="kv-value"]').eq(2).type(`{"email": "${adminEmail}"}`, {
          parseSpecialCharSequences: false,
        });
        cy.get('[data-testid="headers"]').contains("button", "Add headers").click();
        cy.get('[data-testid="headers"] [data-testid="kv-key"]').type("x-e2e-secret");
        cy.get('[data-testid="headers"] [data-testid="kv-value"]').type("${NAHA_E2E_SECRET}", {
          parseSpecialCharSequences: false,
        });
        cy.get('[data-testid="captures"]').contains("button", "Add capture").click();
        cy.get('[data-testid="capture-name"]').type("nahaToken");
        cy.get('[data-testid="capture-from"]').type("json:$.token");
      });

      // Step 2: real NAHA protected endpoint, using the captured Clerk session token as a Bearer
      // credential.
      cy.contains("button", "Add step").click();
      cy.get('[data-testid="step-1"]').within(() => {
        cy.get('[data-testid="step-operation"]').type("http.request");
        cy.get('[data-testid="params"]').contains("button", "Add parameters").click();
        cy.get('[data-testid="params"] [data-testid="kv-key"]').type("url");
        cy.get('[data-testid="params"] [data-testid="kv-value"]').type(`${apiBaseUrl}/api/me`);
        cy.get('[data-testid="headers"]').contains("button", "Add headers").click();
        cy.get('[data-testid="headers"] [data-testid="kv-key"]').type("Authorization");
        cy.get('[data-testid="headers"] [data-testid="kv-value"]').type("Bearer {{nahaToken}}", {
          parseSpecialCharSequences: false,
        });
      });

      // Step 3: assert against role, not email — NAHA's real /api/me response leaves
      // principal.email empty for this e2e-seeded admin user (confirmed empirically); role is the
      // meaningful signal for an admin-gated endpoint.
      cy.contains("button", "Add step").click();
      cy.get('[data-testid="step-2"]').within(() => {
        cy.get('[data-testid="step-operation"]').type("http.assertJsonPath");
        cy.get('[data-testid="params"]').contains("button", "Add parameters").click();
        cy.get('[data-testid="params"] [data-testid="kv-key"]').eq(0).type("path");
        cy.get('[data-testid="params"] [data-testid="kv-value"]').eq(0).type("$.principal.role");
        cy.get('[data-testid="params"]').contains("button", "Add parameters").click();
        cy.get('[data-testid="params"] [data-testid="kv-key"]').eq(1).type("expected");
        cy.get('[data-testid="params"] [data-testid="kv-value"]').eq(1).type("admin");
      });

      cy.get("textarea[readonly]").invoke("val").should("include", "v1/e2e/login");
      cy.screenshot("naha-real-journey/01-pipeline-composed");

      cy.contains("button", "Save as new version").click();
      cy.contains("Saved as version 1.", { timeout: 10000 }).should("be.visible");
      cy.screenshot("naha-real-journey/02-saved");

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
            const { stdout, stderr } = result as { code: number; stdout: string; stderr: string };
            expect(stdout, `stdout:\n${stdout}\nstderr:\n${stderr}`).to.match(new RegExp(`^PASS ${caseId}$`, "m"));
          });
        });
      });
    });
  });
});
