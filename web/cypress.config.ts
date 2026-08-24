import path from "node:path";
import { randomUUID } from "node:crypto";
import { config as loadDotenv } from "dotenv";
import { clerkSetup } from "@clerk/testing/cypress";
import { createClerkClient } from "@clerk/backend";
import { defineConfig } from "cypress";

// web-cypress-e2e design.md: Cypress runs in its own Node process — it doesn't get web/.env.local
// for free the way `next dev` does, so load it explicitly rather than duplicating secrets into a
// second file. CLERK_SECRET_KEY there is already unprefixed and works as-is for @clerk/testing.
loadDotenv({ path: path.resolve(__dirname, ".env.local") });

export default defineConfig({
  // Clerk *development* instances (accounts.dev shared infra, not a custom domain) bounce through
  // their own accounts.dev origin to establish cross-domain session cookies even when using
  // embedded components rather than the Account Portal — this disables Cypress's same-origin
  // enforcement so that bounce doesn't need `cy.origin()` gymnastics. A production Clerk instance
  // on a real custom domain wouldn't need this; verify if this project ever moves off a dev instance.
  chromeWebSecurity: false,
  e2e: {
    baseUrl: "http://localhost:3000",
    async setupNodeEvents(on, config) {
      config = await clerkSetup({
        config,
        // clerkSetup() parses the Frontend API domain from the publishable key itself — no need to
        // also pass frontendApiUrl separately (verified against @clerk/testing's own source).
        options: { publishableKey: process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY },
      });

      on("task", {
        // Idempotent by construction: looks up the test user before ever creating one, so running
        // this task repeatedly (every local/CI run) never creates duplicates.
        //
        // Reads from config.env (populated from cypress.env.json / CYPRESS_* env vars), not
        // process.env — Cypress does not forward cypress.env.json into the Node process's own
        // process.env, only into this config object.
        async ensureE2ETestUser() {
          const email: string | undefined = config.env.E2E_TEST_USER_EMAIL;
          if (!email) {
            throw new Error("E2E_TEST_USER_EMAIL must be set in cypress.env.json (see cypress.env.json.example).");
          }
          if (!email.includes("+clerk_test@")) {
            throw new Error(
              "E2E_TEST_USER_EMAIL must use Clerk's '+clerk_test@' test-address convention (e.g. releasetwin-e2e-test+clerk_test@gmail.com) — " +
                "see design.md: this is what makes email_code sign-in work with a fixed, known code and no real delivery.",
            );
          }

          const clerkClient = createClerkClient({ secretKey: process.env.CLERK_SECRET_KEY });
          const existing = await clerkClient.users.getUserList({ emailAddress: [email] });
          if (existing.data.length > 0) {
            return { created: false, userId: existing.data[0].id };
          }

          // This instance requires a password at creation time regardless of sign-in strategy — but
          // the test actually signs in via email_code, which Clerk's Device Trust feature (an
          // auto-required second factor for *password* sign-ins from a new device — see design.md)
          // does not apply to at all. Automated test runs are always "a new device" to Clerk, so
          // password sign-in has no supported bypass; email_code sidesteps the problem entirely —
          // this password is set only to satisfy account creation, never used to sign in.
          const user = await clerkClient.users.createUser({
            emailAddress: [email],
            password: randomUUID() + randomUUID(),
            skipPasswordChecks: true,
          });
          return { created: true, userId: user.id };
        },
      });

      return config;
    },
  },
});
