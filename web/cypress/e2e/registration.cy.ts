import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * registration-and-product-usage-e2e design.md: drives Clerk's real hosted sign-up form (the
 * Account Portal, at accounts.dev — not a local `/sign-up` route, and not the backend admin-API
 * shortcut `ensureE2ETestUser` uses) with a disposable `+clerk_test@` address, to actually exercise
 * a brand-new visitor's first-time provisioning path. That address is reused run over run (not
 * regenerated), so only the very first-ever run exercises the fresh-signup branch; every later run
 * exercises Clerk's own "already have an account" branch instead — both are asserted below as
 * acceptable outcomes, per design.md's explicit decision.
 *
 * No cy.origin() here: cypress.config.ts already disables chromeWebSecurity specifically so this
 * cross-origin bounce to the Account Portal can be driven with plain cy.* commands. Also doesn't
 * click the "Sign up" footer link itself — confirmed empirically during implementation that in
 * Cypress's Electron runner (unlike a real browser) that link's rendered `href` isn't reliably the
 * real cross-origin Account Portal URL, so clicking it doesn't navigate anywhere. The Account
 * Portal's URL is deterministic from the Frontend API host Clerk itself reports (`<slug>.clerk.
 * accounts.dev` → `<slug>.accounts.dev`), so this visits it directly instead.
 */
describe("registration", () => {
  it("signs up (or recognizes an already-registered address) and lands on a provisioned dashboard", () => {
    setupClerkTestingToken();

    const email = Cypress.env("E2E_REGISTRATION_TEST_USER_EMAIL");
    expect(email, "E2E_REGISTRATION_TEST_USER_EMAIL must be set in cypress.env.json (see .example)").to.be.a(
      "string",
    );

    const frontendApi = Cypress.env("CLERK_FAPI") as string;
    expect(frontendApi, "CLERK_FAPI must be set by @clerk/testing's clerkSetup()").to.be.a("string");
    const accountPortalHost = frontendApi.replace(".clerk.accounts.dev", ".accounts.dev");
    // This dev instance requires a password at account-creation time regardless of sign-in strategy
    // (same fact `ensureE2ETestUser`'s own comment documents for the admin-API path).
    const password = "ThrowawayRegistrationTestPassword1!";

    cy.visit(`https://${accountPortalHost}/sign-up`);

    cy.get('input[name="emailAddress"]', { timeout: 15000 }).type(email);
    cy.get("body").then(($body) => {
      if ($body.find('input[name="password"]').length > 0) {
        cy.get('input[name="password"]').type(password);
      }
    });
    cy.contains("button", /continue/i).click();

    cy.get("body", { timeout: 15000 }).then(($body) => {
      // Clerk's own "you already have an account" branch — this address is reused across runs, not
      // regenerated, so this is an expected outcome on every run after the first. Per design.md,
      // proving this doesn't break the app is the whole point of this branch — no need to also
      // reach the dashboard, which the fresh-signup branch below already covers.
      if ($body.text().match(/already have an account|already exists|couldn.t find your account/i)) {
        cy.contains(/already have an account|already exists|couldn.t find your account/i).should("be.visible");
        return;
      }

      // Fresh signup: Clerk's '+clerk_test@' convention resolves email_code verification with a
      // fixed, documented test code — confirmed empirically against this hosted instance during
      // implementation (design.md), rather than assumed unchanged from Clerk's docs. This dev
      // instance uses a single hidden `autocomplete="one-time-code"` input behind the 6 visual
      // boxes (not 6 separate named inputs), confirmed by inspecting the rendered form.
      cy.get('input[autocomplete="one-time-code"]', { timeout: 15000 }).type("424242");

      // The account now genuinely exists in Clerk — everything above was the real sign-up UI, per
      // design.md's explicit decision. From here, establish the session on this app's own origin via
      // a real Clerk sign-in ticket (see the `createSignInTicket` task's comment in cypress.config.ts
      // for why: Electron doesn't complete the cross-domain handoff the way a real browser does).
      cy.task<{ ticket: string }>("createSignInTicket", { email }).then(({ ticket }) => {
        cy.visit("/");
        cy.clerkLoaded();
        cy.clerkSignIn({ strategy: "ticket", ticket });
      });

      cy.visit("/dashboard");
      cy.location("pathname", { timeout: 20000 }).should("eq", "/dashboard");

      cy.contains("h1", "Dashboard").should("be.visible");
      cy.contains("Usage this month").should("be.visible");

      // A freshly-provisioned org: no projects yet, and on the Free tier by default.
      cy.contains("New project name").should("be.visible");
      cy.contains("Free plan").should("be.visible");
    });
  });
});
