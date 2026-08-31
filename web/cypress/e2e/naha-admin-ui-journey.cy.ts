import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * ui-journey-visual-evidence (B): the real-target UI journey. Against NAHA's deployed admin app
 * (`e2e-admin` Vercel alias), a ReleaseTwin journey seeds the `naha_e2e_role=admin` cookie its
 * E2E-auth middleware gates on, drives the admin UI in a real browser, bridges into an API leg
 * using NAHA's own `/v1/e2e/login` Bearer, and asserts — with evidence capture on, so the redacted
 * NAHA admin screenshots render on the ReleaseTwin dashboard.
 *
 * Real NAHA config (the `e2e-admin` alias URL, the role cookie, the API base URL + `x-e2e-secret`,
 * an admin email) comes from AWS Secrets Manager via `fetchNahaAdminUiTarget` — never hardcoded.
 * The `x-e2e-secret` is stored as a ReleaseTwin project secret through the real dashboard form
 * during the test, same as naha-real-journey.cy.ts. Needs Playwright's chromium on the runner.
 */
describe("naha admin ui journey", () => {
  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("drives NAHA admin UI behind a seeded cookie and stores the visual evidence", () => {
    cy.task("fetchNahaAdminUiTarget").then((target) => {
      const { adminUiBaseUrl, roleCookieName, roleCookieValue, apiBaseUrl, e2eAuthSecret, adminEmail } =
        target as {
          adminUiBaseUrl: string;
          roleCookieName: string;
          roleCookieValue: string;
          apiBaseUrl: string;
          e2eAuthSecret: string;
          adminEmail: string;
        };

      setupClerkTestingToken();
      cy.visit("/");
      cy.clerkLoaded();
      cy.clerkSignIn({ strategy: "email_code", identifier: Cypress.env("E2E_TEST_USER_EMAIL") });
      cy.visit("/dashboard");
      cy.contains("h1", "Dashboard").should("be.visible");

      const projectName = `e2e-naha-ui-${Date.now()}`;
      cy.get('input[placeholder="New project name"]').type(projectName);
      cy.contains("button", "Create project").click();
      cy.contains(projectName).should("be.visible");

      cy.get("body").then(($body) => {
        if ($body.text().includes("Free plan")) {
          cy.elevateToTeam();
        }
      });

      cy.contains(`Project secrets — ${projectName}`)
        .parents(".rounded-xl")
        .within(() => {
          cy.get('input[name="name"]').type("NAHA_E2E_SECRET");
          cy.get('input[name="value"]').type(e2eAuthSecret, { log: false });
          cy.contains("button", "Add secret").click();
        });
      cy.contains("Configured by").should("be.visible");

      cy.get('[data-testid="evidence-settings"]').within(() => {
        cy.get('input[name="captureDefault"]').check();
        cy.contains("button", "Save").click();
        cy.contains("Saved.").should("be.visible");
      });

      cy.contains("button", "Issue new token").click();
      cy.contains("New token (shown once, copy it now):")
        .parent()
        .find("code")
        .first()
        .invoke("text")
        .then((token) => cy.wrap(token.trim()).as("apiToken"));

      cy.contains(`Journeys — ${projectName}`).parents(".rounded-xl").contains("Open builder").click();
      cy.contains("h1", "Journeys").should("be.visible");

      const journeyName = `e2e-naha-ui-${Date.now()}`;
      cy.get('input[name="name"]').type(journeyName);
      cy.contains("button", "Create journey").click();
      cy.contains("h1", journeyName).should("be.visible");

      const caseId = `E2E-NAHA-UI-${Date.now()}`;
      cy.get('input[placeholder="MY-JOURNEY-1"]').type(caseId);
      cy.get('input[placeholder="tickets/MY-JOURNEY-1"]').type("naha.backend/docs/api-auth.md");
      cy.get('input[placeholder="example.json"]').type("e2e-naha-real-journey.json");

      const stepParam = (stepIndex: number, key: string, value: string, kvIndex: number) => {
        cy.get(`[data-testid="step-${stepIndex}"] [data-testid="params"]`).contains("button", "Add parameters").click();
        cy.get(`[data-testid="step-${stepIndex}"] [data-testid="params"] [data-testid="kv-key"]`).eq(kvIndex).type(key);
        cy.get(`[data-testid="step-${stepIndex}"] [data-testid="params"] [data-testid="kv-value"]`)
          .eq(kvIndex)
          .type(value, { parseSpecialCharSequences: false });
      };

      const stepHeader = (stepIndex: number, key: string, value: string) => {
        cy.get(`[data-testid="step-${stepIndex}"] [data-testid="headers"]`).contains("button", "Add headers").click();
        cy.get(`[data-testid="step-${stepIndex}"] [data-testid="headers"] [data-testid="kv-key"]`).type(key);
        cy.get(`[data-testid="step-${stepIndex}"] [data-testid="headers"] [data-testid="kv-value"]`)
          .type(value, { parseSpecialCharSequences: false });
      };

      const addStep = (stepIndex: number, operation: string) => {
        cy.contains("button", "Add step").click();
        cy.get(`[data-testid="step-${stepIndex}"] [data-testid="step-operation"]`).type(operation);
      };

      // UI legs — behind a seeded auth cookie.
      addStep(0, "ui.setCookie");
      stepParam(0, "name", roleCookieName, 0);
      stepParam(0, "value", roleCookieValue, 1);
      stepParam(0, "url", adminUiBaseUrl, 2);

      addStep(1, "ui.navigate");
      stepParam(1, "url", `${adminUiBaseUrl}/`, 0);

      addStep(2, "ui.assertVisible");
      stepParam(2, "selector", '[data-testid="admin-home"]', 0);

      // Tour the admin app — companies, then policies. E2E-auth mode (NAHA #68) forces the
      // company-branch / policy UI gates open, so these routes render behind the seeded cookie.
      // Each leg: navigate → assert the page shell → wait on the same shell so Act 2 dwells a beat
      // on the rendered screen. The shell testid wraps both the loaded list and the API-error
      // state, so the journey stays green whichever the live NAHA API returns for the e2e context.
      addStep(3, "ui.navigate");
      stepParam(3, "url", `${adminUiBaseUrl}/companies`, 0);

      addStep(4, "ui.assertVisible");
      stepParam(4, "selector", '[data-testid="companies-page"]', 0);

      addStep(5, "ui.waitFor");
      stepParam(5, "selector", '[data-testid="companies-page"]', 0);

      addStep(6, "ui.navigate");
      stepParam(6, "url", `${adminUiBaseUrl}/policies`, 0);

      addStep(7, "ui.assertVisible");
      stepParam(7, "selector", '[data-testid="policies-page"]', 0);

      addStep(8, "ui.waitFor");
      stepParam(8, "selector", '[data-testid="policies-page"]', 0);

      // API bridge — NAHA's own e2e login, then a protected endpoint.
      addStep(9, "http.request");
      stepParam(9, "url", `${apiBaseUrl}/v1/e2e/login`, 0);
      stepParam(9, "method", "POST", 1);
      stepParam(9, "body", `{"email": "${adminEmail}"}`, 2);
      stepHeader(9, "x-e2e-secret", "${NAHA_E2E_SECRET}");
      cy.get('[data-testid="step-9"] [data-testid="captures"]').contains("button", "Add capture").click();
      cy.get('[data-testid="step-9"] [data-testid="capture-name"]').type("nahaToken");
      cy.get('[data-testid="step-9"] [data-testid="capture-from"]').type("json:$.token");

      addStep(10, "http.request");
      stepParam(10, "url", `${apiBaseUrl}/api/me`, 0);
      stepHeader(10, "Authorization", "Bearer {{nahaToken}}");

      addStep(11, "http.assertJsonPath");
      stepParam(11, "path", "$.principal.role", 0);
      stepParam(11, "expected", "admin", 1);

      cy.contains("button", "Add cleanup step").click();
      cy.get('input[placeholder="ui.closePage"]').type("ui.closePage");

      cy.get("textarea[readonly]").invoke("val").should("include", "ui.setCookie");
      cy.screenshot("naha-admin-ui-journey/01-journey-composed");

      cy.contains("button", "Save as new version").click();
      cy.contains("Saved as version 1.", { timeout: 10000 }).should("be.visible");

      cy.location("pathname").then((pathname) => {
        const journeyId = pathname.split("/").pop()!;
        cy.get("@apiToken").then((token) => {
          cy.task(
            "runCliJourney",
            {
              token,
              apiUrl: "http://localhost:5199",
              journeyRef: `${journeyId}@1`,
              env: { RELEASETWIN_UI_ENABLED: "1", RELEASETWIN_EVIDENCE: "on" },
            },
            { timeout: 240000 },
          ).then((result: unknown) => {
            const { stdout, stderr } = result as { code: number; stdout: string; stderr: string };
            const detail = `stdout:\n${stdout}\nstderr:\n${stderr}`;
            expect(stdout, detail).to.match(new RegExp(`^PASS ${caseId}$`, "m"));
            expect(stdout, detail).not.to.match(/evidence not accepted/);
          });
        });
      });

      cy.visit("/dashboard");
      cy.contains(projectName).click();
      cy.contains("tr", caseId).contains("a", "View").click();

      cy.contains("h1", "Run evidence").should("be.visible");
      cy.contains("td code", "ui.setCookie").should("be.visible");
      cy.contains("td code", "ui.navigate").should("be.visible");
      cy.contains("tr", "http.assertJsonPath").should("contain.text", "$.principal.role");
      cy.contains("Screenshots").should("be.visible");
      cy.get("img[alt*='screenshot']")
        .should("have.length.greaterThan", 0)
        .and(($imgs) => {
          $imgs.each((_, img) => expect((img as HTMLImageElement).naturalWidth).to.be.greaterThan(0));
        });
      cy.screenshot("naha-admin-ui-journey/02-naha-admin-evidence");
    });
  });
});
