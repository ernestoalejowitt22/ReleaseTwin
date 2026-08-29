import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * dashboard-staleness-e2e-real-uploads: real sign-in, real project, a real token, and real CLI
 * uploads (via the `runCli` task, same as product-usage-loop.cy.ts) — no seeded/backdated data.
 * `UploadStalenessCalculator.IsStale` compares a ratio (gap since last upload vs. 3x the typical
 * gap between uploads), not an absolute duration, so a real cadence measured in seconds exercises
 * the exact same logic a real customer's multi-day cadence would, without needing to fabricate
 * history the real ingest path can't produce (it always stamps `UploadedAt` with `UtcNow`).
 *
 * A single project is reused across assertions (create -> stale -> fresh) rather than one project
 * per scenario: the Free-tier plan-tier-gating limit caps an org at 1 project, and this test's org
 * is shared with every other e2e spec run in the same suite.
 */
describe("dashboard staleness banner", () => {
  const apiUrl = "http://localhost:5199";
  const casesDir = "examples/cases-http-only";

  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("shows the banner once uploads go quiet, and clears it once uploads resume", () => {
    setupClerkTestingToken();
    cy.visit("/");
    cy.clerkLoaded();
    cy.clerkSignIn({
      strategy: "email_code",
      identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
    });
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");

    const projectName = `e2e-staleness-${Date.now()}`;
    // Scoped by placeholder, not just `[name="name"]` — once a project is selected, its "Set up"
    // section's project-secrets add-secret form also has an `input[name="name"]` on this same page.
    cy.get('input[placeholder="New project name"]').type(projectName);
    cy.contains("button", "Create project").click();
    cy.get("body").then(($body) => {
      if ($body.text().includes("Free plan is limited to 1 project")) {
        cy.contains("a", /^e2e-/).first().click();
      } else {
        cy.contains(projectName).should("be.visible");
      }
    });
    cy.contains("Uploads have gone quiet").should("not.exist");

    cy.contains("button", "Issue new token").click();
    cy.contains("New token (shown once, copy it now):").should("be.visible");
    cy.contains("New token (shown once, copy it now):")
      .parent()
      .find("code")
      .first()
      .invoke("text")
      .then((token) => cy.wrap(token.trim()).as("apiToken"));

    // 5 real CLI uploads, ~2s apart -> a real, tight, but well-defined cadence (median gap ~2s).
    // The first invocation implicitly triggers a `dotnet build`, hence the generous timeout on
    // every call, not just the first — CI cold-start time can vary.
    cy.get("@apiToken").then((token) => {
      for (let i = 0; i < 5; i++) {
        cy.task("runCli", { token, apiUrl, casesDir }, { timeout: 180000 }).then((result) => {
          const { code, stdout, stderr } = result as { code: number; stdout: string; stderr: string };
          expect(code, `runCli failed:\nstdout:\n${stdout}\nstderr:\n${stderr}`).to.eq(0);
        });
        if (i < 4) {
          cy.wait(2000);
        }
      }
    });

    // Well past 3x the ~2s real cadence just established — generous headroom, not a tight margin
    // (design.md: CI runner jitter could otherwise stretch the real cadence unpredictably).
    cy.wait(20000);
    cy.reload();
    cy.contains(`Uploads have gone quiet for ${projectName}.`).should("be.visible");
    cy.contains("RELEASETWIN_API_TOKEN").should("be.visible");
    cy.screenshot("dashboard-staleness-banner/01-stale-project-shows-banner");

    // One more real upload moves the project's most recent upload back to "now" -> no longer
    // stale, even though the earlier, now-old uploads are still part of its history.
    cy.get("@apiToken").then((token) => {
      cy.task("runCli", { token, apiUrl, casesDir }, { timeout: 180000 }).then((result) => {
        const { code, stdout, stderr } = result as { code: number; stdout: string; stderr: string };
        expect(code, `runCli failed:\nstdout:\n${stdout}\nstderr:\n${stderr}`).to.eq(0);
      });
    });

    cy.reload();
    cy.contains(`Uploads have gone quiet for ${projectName}.`).should("not.exist");
    cy.screenshot("dashboard-staleness-banner/02-banner-clears-after-new-upload");
  });
});
