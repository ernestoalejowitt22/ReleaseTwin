import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * web-cypress-e2e: automates the exact walkthrough already verified by hand this session — real
 * Clerk sign-in, real dashboard, real API calls between web/ and the hosted API. GitHub Connections
 * is explicitly excluded (no registered OAuth App for that flow yet — see proposal.md).
 */
describe("dashboard walkthrough", () => {
  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("signs in, creates a project, issues a token, and signs out", () => {
    setupClerkTestingToken();
    // Visit an unprotected page first, per @clerk/testing's own documented pattern — visiting a
    // protected route before signing in would trigger dashboard/page.tsx's auth.protect() redirect
    // to Clerk's hosted sign-in first, instead of letting clerkSignIn drive the flow directly.
    cy.visit("/");
    cy.clerkLoaded();
    // email_code, not password: Clerk's Device Trust feature auto-requires a second factor for
    // password sign-ins from any new device — which every automated test run is, with no supported
    // bypass (verified against Clerk's own docs). email_code sidesteps it entirely, and Clerk's
    // "+clerk_test@" test-address convention (design.md) resolves with a fixed, known code — no
    // real email delivery involved.
    cy.clerkSignIn({
      strategy: "email_code",
      identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
    });
    cy.visit("/dashboard");

    cy.contains("h1", "Dashboard").should("be.visible");
    cy.contains("Projects").should("be.visible");
    // usage-metering: org-wide usage summary, shown regardless of project selection.
    cy.contains("Usage this month").should("be.visible");
    cy.contains("case reports").should("be.visible");
    cy.contains("flag-proof reports").should("be.visible");
    cy.screenshot("dashboard-walkthrough/01-signed-in");

    const projectName = `e2e-project-${Date.now()}`;
    // Scoped by placeholder, not just `[name="name"]` — once a project is selected, its "Set up"
    // section's project-secrets add-secret form also has an `input[name="name"]` on this same page.
    cy.get('input[placeholder="New project name"]').type(projectName);
    cy.contains("button", "Create project").click();

    cy.contains(projectName).should("be.visible");
    cy.contains(`Connection — ${projectName}`).should("be.visible");
    cy.screenshot("dashboard-walkthrough/02-project-created");

    // plan-tier-gating: real exercise of the Free-tier limit — this org (assuming a fresh backing
    // store, or one where this org hasn't been upgraded by an earlier run) now has exactly the one
    // project just created, so a second attempt should be rejected with the real 403 path.
    cy.get("body").then(($body) => {
      if ($body.text().includes("Free plan")) {
        cy.contains("Free plan").should("be.visible");

        const secondProjectName = `e2e-project-${Date.now()}-2`;
        cy.get('input[placeholder="New project name"]').type(secondProjectName);
        cy.contains("button", "Create project").click();
        cy.contains("Free plan is limited to 1 project").should("be.visible");
        cy.contains(secondProjectName).should("not.exist");
        cy.screenshot("dashboard-walkthrough/02b-project-limit-rejected");

        cy.contains("button", "Upgrade").click();
        cy.contains("Paid plan").should("be.visible");
        cy.contains("button", "Upgrade").should("not.exist");
        cy.screenshot("dashboard-walkthrough/02c-upgraded");

        cy.get('input[placeholder="New project name"]').type(secondProjectName);
        cy.contains("button", "Create project").click();
        cy.contains(secondProjectName).should("be.visible");
        cy.screenshot("dashboard-walkthrough/02d-second-project-after-upgrade");
      } else {
        // Already Paid from an earlier run against a persistent backing store — the limit doesn't
        // apply, nothing further to exercise here.
        cy.contains("Paid plan").should("be.visible");
      }
    });

    cy.contains("button", "Issue new token").click();
    cy.contains("New token (shown once, copy it now):").should("be.visible");
    cy.get("code").contains(/^rtw_/).should("be.visible");
    // token-onboarding: install/run instructions and the free-vs-linked optionality statement now
    // ship alongside the token itself, not just the bare value.
    cy.contains("Set it and run a first case:").should("be.visible");
    cy.contains("export RELEASETWIN_API_TOKEN=").should("be.visible");
    cy.contains("dotnet run --project src/ReleaseTwin.Cli -- examples/cases").should("be.visible");
    cy.contains("keeps everything fully local and free").should("be.visible");
    cy.screenshot("dashboard-walkthrough/03-token-issued");

    cy.clerkSignOut();
    cy.visit("/dashboard");
    cy.location("pathname", { timeout: 10000 }).should("not.eq", "/dashboard");
    cy.screenshot("dashboard-walkthrough/04-signed-out");
  });
});
