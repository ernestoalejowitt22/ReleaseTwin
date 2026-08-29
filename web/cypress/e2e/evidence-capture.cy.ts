import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * dashboard-evidence-viewer: end-to-end proof of the opt-in evidence path — enable evidence capture
 * for a Paid-tier project through the real settings form, run the real CLI with RELEASETWIN_EVIDENCE=on
 * (`dotnet run`, via the `runCli` task), and confirm the redacted evidence renders on the dashboard.
 * Also confirms CLI-side redaction: the `Authorization` header the auth-chain example sends never
 * reaches the stored/rendered evidence.
 *
 * Reuses E2E_TEST_USER_EMAIL / ensureE2ETestUser, same as product-usage-loop.cy.ts.
 */
describe("evidence capture loop", () => {
  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("captures, redacts, uploads and renders run evidence", () => {
    setupClerkTestingToken();
    cy.visit("/");
    cy.clerkLoaded();
    cy.clerkSignIn({
      strategy: "email_code",
      identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
    });
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");

    const projectName = `e2e-evidence-${Date.now()}`;
    cy.get('input[placeholder="New project name"]').type(projectName);
    cy.contains("button", "Create project").click();
    cy.contains(projectName).should("be.visible");

    // Evidence storage is Paid-tier only — upgrade if this org is still on Free.
    cy.get("body").then(($body) => {
      if ($body.text().includes("Free plan")) {
        cy.contains("button", "Upgrade").click();
        cy.contains("Paid plan").should("be.visible");
      }
    });

    // Enable evidence capture for this project through the real settings form.
    cy.get('[data-testid="evidence-settings"]').within(() => {
      cy.get('input[name="captureDefault"]').check();
      cy.get('input[name="retentionDays"]').clear().type("14");
      cy.contains("button", "Save").click();
      cy.contains("Saved.").should("be.visible");
    });
    cy.screenshot("evidence-capture/01-settings-enabled");

    cy.contains("button", "Issue new token").click();
    cy.contains("New token (shown once, copy it now):")
      .parent()
      .find("code")
      .first()
      .invoke("text")
      .then((token) => {
        cy.wrap(token.trim()).as("apiToken");
      });

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
      ).then((result: unknown) => {
        const { stdout, stderr } = result as { code: number; stdout: string; stderr: string };
        const detail = `stdout:\n${stdout}\nstderr:\n${stderr}`;
        expect(stdout, detail).to.match(/^PASS HTTP-DEMO-1$/m);
        expect(stdout, detail).to.match(/^PASS AUTH-CHAIN-DEMO-1$/m);
        // Evidence was accepted (Paid tier) — no "not accepted" / upload-failed warnings.
        expect(stdout, detail).not.to.match(/evidence not accepted/);
        expect(stdout, detail).not.to.match(/upload failed for (HTTP-DEMO-1|AUTH-CHAIN-DEMO-1)/);
      });
    });

    cy.location("search").then((search) => {
      const projectId = new URLSearchParams(search).get("projectId");
      expect(projectId, "selected projectId").to.be.a("string");
      cy.wrap(projectId).as("projectId");
    });

    function openDashboard() {
      cy.get<string>("@projectId").then((projectId) => {
        cy.visit(`/dashboard?projectId=${projectId}`);
      });
      cy.contains("h1", "Dashboard").should("be.visible");
    }

    // --- HTTP-DEMO-1: ordered steps + assertion detail render ---
    openDashboard();
    cy.contains("tr", "HTTP-DEMO-1").contains("a", "View").click();

    cy.contains("h1", "Run evidence").should("be.visible");
    cy.contains("Redacted by your CLI before upload").should("be.visible");
    cy.contains("Screenshots are best-effort-redacted").should("be.visible");
    cy.contains("td code", "http.request").should("be.visible");
    cy.contains("tr", "http.assertJsonPath").should("contain.text", "$.id");
    cy.contains("tr", "http.assertJsonPath").should("contain.text", "expected");
    cy.screenshot("evidence-capture/02-http-demo-evidence");

    // --- AUTH-CHAIN-DEMO-1: the Authorization header was stripped in the CLI before upload ---
    openDashboard();
    cy.contains("tr", "AUTH-CHAIN-DEMO-1").contains("a", "View").click();
    cy.contains("h1", "Run evidence").should("be.visible");
    cy.get("details").each(($d) => cy.wrap($d).find("summary").click());
    cy.get("pre").then(($pres) => {
      const all = [...$pres].map((el) => el.textContent ?? "").join("\n");
      // The bearer step's Authorization header was stripped by the CLI's built-in denylist.
      expect(all, "authorization header masked").to.match(/"Authorization":\s*"«redacted»"/);
      expect(all, "no bearer token value in evidence").not.to.match(/Bearer [0-9a-f-]{8,}/i);
    });
    cy.screenshot("evidence-capture/03-auth-header-redacted");
  });
});
