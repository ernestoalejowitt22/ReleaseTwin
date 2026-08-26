import { setupClerkTestingToken } from "@clerk/testing/cypress";

/**
 * e2e-github-connection-flow: drives the real, unmocked GitHub OAuth round trip —
 * ConnectionEndpoints.MapConnectionEndpoints's /start, the real github.com login + consent screen,
 * GitHubConnectionFlowService.ExchangeCodeForRepositoriesAsync's real token exchange and repo
 * listing, and /confirm — against the project owner's real account and real repo
 * (ernestoalejowitt22/NAHA). See design.md for why this needs a second, localhost-only OAuth App
 * and why the account's password/TOTP secret come from AWS Secrets Manager rather than
 * cypress.env.json.
 */
describe("github connection flow", () => {
  const externalRepo = "ernestoalejowitt22/NAHA";

  before(() => {
    cy.task("ensureE2ETestUser");
  });

  it("connects a project to a real GitHub repo via the real OAuth flow", () => {
    setupClerkTestingToken();
    cy.visit("/");
    cy.clerkLoaded();
    cy.clerkSignIn({
      strategy: "email_code",
      identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
    });
    cy.visit("/dashboard");
    cy.contains("h1", "Dashboard").should("be.visible");

    // plan-tier-gating: this org may already be at its Free-tier project limit from another spec
    // run earlier in the same suite — reuse the existing project in that case rather than failing.
    const projectName = `e2e-github-${Date.now()}`;
    cy.get('input[name="name"]').type(projectName);
    cy.contains("button", "Create project").click();
    cy.get("body").then(($body) => {
      if ($body.text().includes("Free plan is limited to 1 project")) {
        cy.contains("a", /^e2e-/).first().click();
      } else {
        cy.contains(projectName).should("be.visible");
      }
    });

    cy.contains("button", "Connect GitHub").click();

    cy.task("fetchGitHubTestAccount").then((account) => {
      const { username, password, currentTotpCode } = account as {
        username: string;
        password: string;
        currentTotpCode: string;
      };

      cy.origin(
        "https://github.com",
        { args: { username, password, currentTotpCode } },
        ({ username, password, currentTotpCode }) => {
          cy.get("#login_field", { timeout: 15000 }).type(username);
          cy.get("#password").type(password);
          // Scoped to the actual login form, not a loose page-wide match — GitHub's login page
          // has a second "Sign in" element outside the form (a header/nav link) that also matches
          // a bare `input[type="submit"][value="Sign in"]` selector.
          cy.get("#password").closest("form").find('input[type="submit"]').click();

          // Only present when GitHub actually challenges this login for 2FA — a session GitHub
          // already trusts (e.g. a recent prior run from the same machine) can skip straight to
          // the consent screen instead.
          cy.location("pathname", { timeout: 15000 }).then((pathname) => {
            if (pathname.includes("two-factor")) {
              cy.get("#app_totp").type(currentTotpCode);
            }
          });

          // First-time authorization only — GitHub skips this screen on every later run once the
          // account has already approved this OAuth App.
          cy.get("body", { timeout: 15000 }).then(($body) => {
            if ($body.find('button[name="authorize"][value="1"]').length > 0) {
              cy.get('button[name="authorize"][value="1"]').click();
            }
          });
        },
      );
    });

    // The real GitHub round trip (login + 2FA + consent, each a genuine network round trip) can
    // take longer than this Clerk dev instance's session token survives — confirmed empirically:
    // GitHub issues a real, valid authorization code, but by the time the browser lands back on
    // this app, `auth.protect()` on the callback page finds no valid session and bounces to a
    // sign-in page — either this app's own `/sign-in` route or, sometimes, Clerk's accounts.dev-
    // hosted one — either way preserving the original destination (code and state intact) via a
    // `redirect_url` param. Re-authenticating and resuming there directly is exactly what a real
    // customer hitting this would need to do — GitHub's authorization code is still valid for
    // several minutes, unused.
    // Wait for the redirect chain to actually settle on one of its two possible terminal states
    // before reading it — reading immediately as cy.origin() exits can catch it mid-flight.
    cy.location("pathname", { timeout: 20000 }).should((pathname) => {
      expect(["/connect/github/callback", "/sign-in"]).to.include(pathname);
    });

    cy.location("href").then((href) => {
      const url = new URL(href);
      if (url.pathname === "/sign-in") {
        const resumeUrl = url.searchParams.get("redirect_url");
        expect(resumeUrl, "redirect_url should carry the original GitHub callback").to.be.a("string");

        setupClerkTestingToken();
        cy.visit("/");
        cy.clerkLoaded();
        // `/sign-in` can be reached transiently without the session actually being gone (Clerk's
        // own redirect dance briefly routes through this path) — clerkSignIn() throws "You're
        // already signed in" if called while a session is still live, so only call it when one
        // genuinely isn't.
        cy.window().then((win) => {
          const clerk = (win as unknown as { Clerk?: { session?: unknown } }).Clerk;
          if (!clerk?.session) {
            cy.clerkSignIn({
              strategy: "email_code",
              identifier: Cypress.env("E2E_TEST_USER_EMAIL"),
            });
          }
        });
        cy.visit(resumeUrl!);
      }
    });

    // Real redirect back to this app's own origin — GitHubCallbackPage exchanges the code
    // server-side and renders the real repo list returned by GitHub.
    cy.location("pathname", { timeout: 20000 }).should("eq", "/connect/github/callback");
    cy.contains("Choose a repository").should("be.visible");
    cy.contains("label", externalRepo).should("be.visible").click();
    cy.contains("button", "Connect this repository").click();

    cy.location("pathname", { timeout: 10000 }).should("eq", "/dashboard");
    cy.contains(`Connected to`).should("be.visible");
    cy.contains("code", externalRepo).should("be.visible");
    cy.contains("(github)").should("be.visible");
    cy.screenshot("github-connection/01-connection-confirmed");
  });
});
