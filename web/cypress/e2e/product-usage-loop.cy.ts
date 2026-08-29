import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * registration-and-product-usage-e2e design.md: closes the loop that every other spec only seeds
 * server-side — issue a real token through the dashboard UI, actually run the CLI (`dotnet run`,
 * via the `runCli` task in cypress.config.ts) against it, and confirm the dashboard reflects a real
 * upload. Reuses `E2E_TEST_USER_EMAIL`/`ensureE2ETestUser`, same as dashboard-walkthrough.cy.ts —
 * this spec's concern is the CLI integration loop, not auth/provisioning (that's registration.cy.ts).
 */
describe("product usage loop", () => {
  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("runs the CLI against a dashboard-issued token and sees the result reflected back", () => {
    setupClerkTestingToken();
    cy.visit("/");
    cy.clerkLoaded();
    cy.clerkSignIn({
      strategy: "email_code",
      identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
    });
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");

    const projectName = `e2e-usage-loop-${Date.now()}`;
    // Scoped by placeholder, not just `[name="name"]` — once a project is selected, its "Set up"
    // section's project-secrets add-secret form also has an `input[name="name"]` on this same page.
    cy.get('input[placeholder="New project name"]').type(projectName);
    cy.contains("button", "Create project").click();
    cy.contains(projectName).should("be.visible");

    // Case-report usage count baseline, captured before the CLI ever runs — the org-wide counter
    // shown regardless of which project is selected (usage-metering).
    cy.contains("case reports")
      .parent()
      .find("p.text-2xl")
      .invoke("text")
      .then((text) => {
        const baseline = Number(text.trim());
        expect(baseline, "baseline case report count should be a number").to.be.a("number");
        cy.wrap(baseline).as("baselineCaseReportCount");
      });

    cy.contains("button", "Issue new token").click();
    cy.contains("New token (shown once, copy it now):").should("be.visible");
    // Scoped to the "shown once" box specifically (not just any `<code>` starting with `rtw_`) —
    // issuing a token triggers a server-action revalidation, so the tokens table above it can pick
    // up the same new token as a *second*, truncated `<code>rtw_xxx…</code>` row in the same render;
    // an unscoped `.contains(/^rtw_/)` can match that truncated row instead of the real one.
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
        },
        { timeout: 180000 },
      ).then((result: unknown) => {
        const { stdout, stderr } = result as { code: number; stdout: string; stderr: string };
        // Not asserting exit code 0 here: `examples/cases` also bundles the Azure DevOps and
        // flag-proof examples, which the CLI (correctly) fails without their own credentials/adapter
        // config — exactly the command a real customer would copy-paste (design.md), but not a
        // hermetic zero-credential run overall. What this test actually needs is that the bundled
        // zero-credential HTTP example specifically passed and uploaded — confirmed via stdout.
        expect(stdout, `stdout:\n${stdout}\nstderr:\n${stderr}`).to.match(/^PASS HTTP-DEMO-1$/m);
        expect(stdout, `stdout:\n${stdout}\nstderr:\n${stderr}`).not.to.match(/upload failed for HTTP-DEMO-1/);
      });
    });

    cy.reload();
    cy.contains(projectName).should("be.visible");
    cy.contains("HTTP-DEMO-1").should("be.visible");

    // The case-report counter increases by 3, not 1: running the real, bundled `examples/cases`
    // directory (matching exactly what the dashboard tells a customer to copy-paste) also executes
    // `example-claim.yaml` (CLM-042), which fails without Azure DevOps credentials but still uploads
    // as a regular case report, and `example-auth-chain.yaml` (AUTH-CHAIN-DEMO-1), a zero-credential
    // HTTP case that passes and uploads like HTTP-DEMO-1 — both confirmed empirically. `example-
    // flag-proof.yaml`'s case is skipped before upload entirely (no Azure DevOps adapter installed),
    // so it doesn't affect this counter.
    cy.get("@baselineCaseReportCount").then((baseline) => {
      cy.contains("case reports")
        .parent()
        .find("p.text-2xl")
        .invoke("text")
        .then((text) => {
          const updated = Number(text.trim());
          expect(updated).to.eq((baseline as unknown as number) + 3);
        });
    });
  });
});
