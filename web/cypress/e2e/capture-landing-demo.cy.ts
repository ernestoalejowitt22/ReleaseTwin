import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * landing-demo-ci-loop: captures the dashboard panels for the marketing landing page from
 * the REAL hosted dashboard — sign in, create a Team project, enable evidence, run the
 * bundled credential-free cases through the real CLI with evidence upload on, then
 * screenshot the run history and the redacted evidence viewer.
 *
 * Not part of `npm run e2e` — run on demand via `npm run capture:dashboard`, which brings
 * up the hosted API + web dev server first. Screenshots land in
 * cypress/screenshots/capture-landing-demo.cy.ts/ and are copied into web/public/demo/ by
 * web/scripts/capture-dashboard-demo.mjs.
 */
describe("landing demo — dashboard panels", () => {
  before(() => {
    cy.task("ensureE2ETestUser");
  });

  // Clerk's hosted UI loads code-split chunks from its dev CDN; those occasionally 404
  // mid-flight. Not our bug, and it shouldn't fail a screenshot run.
  Cypress.on("uncaught:exception", (err) =>
    /ChunkLoadError|Loading chunk .* failed/.test(err.message) ? false : undefined,
  );

  it("captures run history and the redacted evidence viewer", () => {
    setupClerkTestingToken();
    cy.visit("/");
    cy.clerkLoaded();
    cy.clerkSignIn({
      strategy: "email_code",
      identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
    });
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");

    const projectName = `demo-${Date.now()}`;
    cy.get('input[placeholder="New project name"]').type(projectName);
    cy.contains("button", "Create project").click();
    cy.contains(projectName).should("be.visible");

    cy.get("body").then(($body) => {
      if ($body.find("button:contains('Upgrade')").length) {
        cy.contains("button", "Upgrade").first().click();
      }
    });
    cy.contains(/Team plan|Paid plan/).should("be.visible");

    cy.get('[data-testid="evidence-settings"]').within(() => {
      cy.get('input[name="captureDefault"]').check();
      cy.get('input[name="retentionDays"]').clear().type("14");
      cy.contains("button", "Save").click();
      cy.contains("Saved.").should("be.visible");
    });

    cy.contains("button", "Issue new token").click();
    cy.contains("New token (shown once, copy it now):")
      .parent()
      .find("code")
      .first()
      .invoke("text")
      .then((t) => cy.wrap(t.trim()).as("apiToken"));

    cy.get("@apiToken").then((token) => {
      cy.task(
        "runCli",
        {
          token,
          apiUrl: "http://localhost:5199",
          casesDir: "examples/cases",
          env: { RELEASETWIN_EVIDENCE: "on" },
        },
        { timeout: 180000 },
      ).then((r: unknown) => {
        const { stdout, stderr } = r as { stdout: string; stderr: string };
        const detail = `stdout:\n${stdout}\nstderr:\n${stderr}`;
        expect(stdout, detail).to.match(/^PASS HTTP-DEMO-1$/m);
        expect(stdout, detail).to.match(/^PASS AUTH-CHAIN-DEMO-1$/m);
        expect(stdout, detail).not.to.match(/evidence not accepted/);
      });
    });

    cy.location("search").then((search) => {
      const projectId = new URLSearchParams(search).get("projectId");
      expect(projectId, "selected projectId").to.be.a("string");
      cy.wrap(projectId).as("projectId");
    });

    const openDashboard = () =>
      cy.get<string>("@projectId").then((projectId) => {
        cy.visit(`/dashboard?projectId=${projectId}`);
        cy.contains("h1", "Dashboard").should("be.visible");
      });

    // 1 — run history
    openDashboard();
    cy.contains("tr", "HTTP-DEMO-1").should("be.visible");
    cy.contains("tr", "AUTH-CHAIN-DEMO-1").should("be.visible");
    cy.screenshot("runs", { capture: "viewport" });

    // 2 — evidence viewer, showing CLI-side redaction of the Authorization header
    openDashboard();
    cy.contains("tr", "AUTH-CHAIN-DEMO-1").contains("a", "View").click();
    cy.contains("h1", "Run evidence").should("be.visible");
    cy.contains("Redacted by your CLI before upload").should("be.visible");
    cy.get("details").each(($d) => cy.wrap($d).find("summary").click());
    cy.screenshot("evidence", { capture: "viewport" });
  });
});
