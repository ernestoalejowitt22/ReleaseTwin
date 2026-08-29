import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * ui-journey-visual-evidence: the honest end-to-end test of the UI adapter's visual evidence —
 * compose a real browser login journey in the builder, run it via the CLI with the UI adapter and
 * evidence capture both on, and confirm the redacted screenshots render on the dashboard. Also
 * confirms C: the login form's password never reaches the uploaded evidence.
 *
 * Real Clerk sign-in, real dashboard, real builder, real `dotnet run` (RELEASETWIN_UI_ENABLED=1
 * RELEASETWIN_EVIDENCE=on), real third-party page (the-internet.herokuapp.com — a public
 * login-form fixture site). Needs Playwright's chromium installed on the runner.
 */
describe("ui journey visual evidence", () => {
  const PASSWORD = "SuperSecretPassword!";

  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("captures redacted screenshots from a real UI journey and renders them on the dashboard", () => {
    setupClerkTestingToken();
    cy.visit("/");
    cy.clerkLoaded();
    cy.clerkSignIn({ strategy: "email_code", identifier: Cypress.env("E2E_TEST_USER_EMAIL") });
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");

    const projectName = `e2e-ui-evidence-${Date.now()}`;
    cy.get('input[placeholder="New project name"]').type(projectName);
    cy.contains("button", "Create project").click();
    cy.contains(projectName).should("be.visible");

    cy.get("body").then(($body) => {
      if ($body.text().includes("Free plan")) {
        cy.contains("button", "Upgrade").click();
        cy.contains("Paid plan").should("be.visible");
      }
    });

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

    const journeyName = `e2e-ui-evidence-${Date.now()}`;
    cy.get('input[name="name"]').type(journeyName);
    cy.contains("button", "Create journey").click();
    cy.contains("h1", journeyName).should("be.visible");

    const caseId = `E2E-UI-EVIDENCE-${Date.now()}`;
    cy.get('input[placeholder="MY-JOURNEY-1"]').type(caseId);
    cy.get('input[placeholder="tickets/MY-JOURNEY-1"]').type("docs/customer-pilot-guide.md");
    cy.get('input[placeholder="example.json"]').type("example-http.json");

    const stepParam = (stepIndex: number, key: string, value: string, kvIndex: number) => {
      cy.get(`[data-testid="step-${stepIndex}"] [data-testid="params"]`).contains("button", "Add parameters").click();
      cy.get(`[data-testid="step-${stepIndex}"] [data-testid="params"] [data-testid="kv-key"]`).eq(kvIndex).type(key);
      cy.get(`[data-testid="step-${stepIndex}"] [data-testid="params"] [data-testid="kv-value"]`)
        .eq(kvIndex)
        .type(value, { parseSpecialCharSequences: false });
    };

    const addStep = (stepIndex: number, operation: string, params: Array<[string, string]>) => {
      cy.contains("button", "Add step").click();
      cy.get(`[data-testid="step-${stepIndex}"] [data-testid="step-operation"]`).type(operation);
      params.forEach(([k, v], i) => stepParam(stepIndex, k, v, i));
    };

    addStep(0, "ui.navigate", [["url", "https://the-internet.herokuapp.com/login"]]);
    addStep(1, "ui.fill", [["selector", "#username"], ["value", "tomsmith"]]);
    addStep(2, "ui.fill", [["selector", "#password"], ["value", PASSWORD]]);
    addStep(3, "ui.click", [["selector", "button[type='submit']"]]);
    addStep(4, "ui.waitFor", [["selector", "#flash"], ["state", "visible"]]);
    addStep(5, "ui.assertVisible", [["selector", "#flash"]]);
    addStep(6, "http.request", [["url", "https://httpbin.org/get?step=after-login"]]);
    addStep(7, "http.assertJsonPath", [["path", "$.args.step"], ["expected", "after-login"]]);

    cy.contains("button", "Add cleanup step").click();
    cy.get('input[placeholder="ui.closePage"]').type("ui.closePage");

    cy.get("textarea[readonly]").invoke("val").should("include", "ui.navigate");
    cy.screenshot("ui-journey-visual-evidence/01-journey-composed");

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
          expect(stdout, detail).not.to.match(new RegExp(`upload failed for ${caseId}`));
        });
      });
    });

    // Dashboard: the run-history row now has an evidence drill-down.
    cy.visit("/dashboard");
    cy.contains(projectName).click();
    cy.contains("tr", caseId).contains("a", "View").click();

    cy.contains("h1", "Run evidence").should("be.visible");
    cy.contains("Redacted by your CLI before upload").should("be.visible");
    cy.contains("td code", "ui.navigate").should("be.visible");
    cy.contains("td code", "ui.fill").should("be.visible");
    cy.get("details").each(($d) => cy.wrap($d).find("summary").click());
    cy.get("img[alt*='screenshot']").should("have.length.greaterThan", 0);
    cy.screenshot("ui-journey-visual-evidence/02-ui-evidence-with-screenshots");

    // C: the password literal is nowhere in the rendered evidence, and its fill step is flagged
    // ValueIsProtected so no allowlist entry could re-expose it.
    cy.get("body").invoke("text").should("not.contain", PASSWORD);
    cy.get("pre").then(($pres) => {
      const all = [...$pres].map((el) => el.textContent ?? "").join("\n");
      expect(all, "password literal absent").not.to.contain(PASSWORD);
      expect(all, "fill value masked").to.match(/"value":\s*"«redacted»"/);
      expect(all, "password field flagged protected").to.match(/"ValueIsProtected":\s*true/);
    });
    cy.screenshot("ui-journey-visual-evidence/03-password-redacted");
  });
});
