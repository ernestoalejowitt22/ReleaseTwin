import path from "node:path";
import { randomUUID } from "node:crypto";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { config as loadDotenv } from "dotenv";
import { clerkSetup } from "@clerk/testing/cypress";
import { createClerkClient } from "@clerk/backend";
import { SecretsManagerClient, GetSecretValueCommand } from "@aws-sdk/client-secrets-manager";
import { generate as generateTotpCode, createGuardrails } from "otplib";
import { defineConfig } from "cypress";

// web-cypress-e2e design.md: Cypress runs in its own Node process — it doesn't get web/.env.local
// for free the way `next dev` does, so load it explicitly rather than duplicating secrets into a
// second file. CLERK_SECRET_KEY there is already unprefixed and works as-is for @clerk/testing.
loadDotenv({ path: path.resolve(__dirname, ".env.local") });

const execFileAsync = promisify(execFile);

// Resolved from this file's own directory (not process.cwd()), so this task works the same whether
// `npm run e2e`/`cypress run` is invoked from `web/` or the repo root.
const repoRoot = path.resolve(__dirname, "..");
const cliProjectPath = path.join(repoRoot, "src", "ReleaseTwin.Cli");

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
        // e2e-github-connection-flow design.md: the project owner's real GitHub password + TOTP
        // secret live in AWS Secrets Manager, not in any repo-adjacent file (even a gitignored
        // one) — fetched here using whatever AWS credentials are already available in the
        // environment running Cypress, the same "ambient credential chain" the .NET side already
        // relies on for DynamoDB. The TOTP code is generated fresh on every call, never cached,
        // since codes are time-windowed and a stale one would just fail to log in.
        async fetchGitHubTestAccount() {
          const client = new SecretsManagerClient({});
          const response = await client.send(
            new GetSecretValueCommand({ SecretId: "releasetwin/e2e/github-account" }),
          );
          if (!response.SecretString) {
            throw new Error("releasetwin/e2e/github-account has no SecretString value.");
          }

          const { username, password, totpSecret } = JSON.parse(response.SecretString) as {
            username: string;
            password: string;
            totpSecret: string;
          };

          // otplib v13 defaults to a 128-bit minimum secret length (stricter than RFC 4226's own
          // minimum) — this GitHub account's real, GitHub-issued secret predates that and is
          // 80 bits, still a completely valid TOTP secret from GitHub's own perspective.
          const currentTotpCode = await generateTotpCode({
            secret: totpSecret,
            guardrails: createGuardrails({ MIN_SECRET_BYTES: 10 }),
          });

          return { username, password, currentTotpCode };
        },

        // launchdarkly-real-flag-proof: real LaunchDarkly test-account credentials (API token,
        // project key, environment key only — the flag key deliberately lives in the spec file
        // itself, not the secret, so different tests can target different flags) live in AWS
        // Secrets Manager, same convention as fetchGitHubTestAccount above.
        async fetchLaunchDarklyTestAccount() {
          const client = new SecretsManagerClient({});
          const response = await client.send(
            new GetSecretValueCommand({ SecretId: "releasetwin/e2e/launchdarkly-account" }),
          );
          if (!response.SecretString) {
            throw new Error("releasetwin/e2e/launchdarkly-account has no SecretString value.");
          }

          const { apiToken, projectKey, environmentKey } = JSON.parse(response.SecretString) as {
            apiToken: string;
            projectKey: string;
            environmentKey: string;
          };

          return { apiToken, projectKey, environmentKey };
        },

        // launchdarkly-real-flag-proof: writes a throwaway case + fixture directory for a
        // LaunchDarkly flag-proof case targeting a caller-supplied real flag key — generated per
        // run rather than checked in, since the flag key is chosen by the spec, not fixed content.
        async writeLaunchDarklyFlagProofCase({
          directory,
          caseId,
          flagKey,
        }: {
          directory: string;
          caseId: string;
          flagKey: string;
        }) {
          const fs = await import("node:fs/promises");
          const casesDir = path.join(directory, "cases");
          const fixturesDir = path.join(directory, "fixtures");
          await fs.mkdir(casesDir, { recursive: true });
          await fs.mkdir(fixturesDir, { recursive: true });

          await fs.writeFile(path.join(fixturesDir, `${caseId}.json`), "{}\n");

          const yaml = [
            `id: ${caseId}`,
            "oracle:",
            `  locator: tickets/${caseId}`,
            "fixture:",
            `  locator: ${caseId}.json`,
            "requires:",
            "  - http:launchdarkly",
            "pipeline:",
            "  - operation: ld.readFeatureFlag",
            "flag_proof:",
            `  feature_key: ${flagKey}`,
            `  build_identity: ${caseId}-build`,
            "",
          ].join("\n");
          await fs.writeFile(path.join(casesDir, `${caseId}.yaml`), yaml);

          return { casesDir };
        },

        // hosted-project-secrets 7.2: writes a throwaway case referencing `${varName}` in its URL,
        // with no matching local environment variable — the point is forcing CliRunner's hosted
        // project-secrets fetch to be the only thing that can resolve it.
        async writeProjectSecretCase({
          directory,
          caseId,
          varName,
        }: {
          directory: string;
          caseId: string;
          varName: string;
        }) {
          const fs = await import("node:fs/promises");
          const casesDir = path.join(directory, "cases");
          const fixturesDir = path.join(directory, "fixtures");
          await fs.mkdir(casesDir, { recursive: true });
          await fs.mkdir(fixturesDir, { recursive: true });

          await fs.writeFile(path.join(fixturesDir, `${caseId}.json`), "{}\n");

          const yaml = [
            `id: ${caseId}`,
            "oracle:",
            `  locator: tickets/${caseId}`,
            "fixture:",
            `  locator: ${caseId}.json`,
            "pipeline:",
            "  - operation: http.request",
            "    with:",
            `      method: GET`,
            `      url: \${${varName}}/posts/1`,
            "  - operation: http.assertJsonPath",
            "    with:",
            "      path: $.id",
            "      expected: 1",
            "",
          ].join("\n");
          await fs.writeFile(path.join(casesDir, `${caseId}.yaml`), yaml);

          return { casesDir };
        },

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

        // registration design.md: after registration.cy.ts drives the *real* sign-up UI through
        // email + password + email_code verification, Cypress's Electron runner (unlike a real
        // browser) doesn't complete Clerk's cross-domain session handoff back to this app — it
        // bounces to the Account Portal's own "sign in" page instead, confirmed empirically during
        // implementation. This mints a real Clerk sign-in token for the just-created user (Clerk's
        // own documented mechanism for handing a session to a client without re-prompting for
        // credentials — the "ticket" strategy @clerk/testing's clerkSignIn already supports) so the
        // test can establish the session directly on this app's own origin instead. It only ever
        // runs *after* the real sign-up form has already been submitted — it doesn't shortcut any
        // part of account creation itself.
        async createSignInTicket({ email }: { email: string }) {
          const clerkClient = createClerkClient({ secretKey: process.env.CLERK_SECRET_KEY });
          const users = await clerkClient.users.getUserList({ emailAddress: [email] });
          if (users.data.length === 0) {
            throw new Error(`createSignInTicket: no Clerk user found for ${email}`);
          }
          const signInToken = await clerkClient.signInTokens.createSignInToken({
            userId: users.data[0].id,
            expiresInSeconds: 60,
          });
          return { ticket: signInToken.token };
        },

        // product-usage-loop design.md: shells out to a real `dotnet run` of the CLI against a
        // dashboard-issued token, so the e2e suite exercises the actual, unmocked upload path a
        // customer would use — not a seeded/mocked report. A first invocation implicitly triggers a
        // `dotnet build`, so this is intentionally not bounded by Cypress's default command timeout
        // (see the `taskTimeout` passed alongside `cy.task('runCli', ...)` in the spec itself).
        async runCli({
          token,
          apiUrl,
          casesDir,
          env,
        }: {
          token: string;
          apiUrl: string;
          casesDir: string;
          env?: Record<string, string>;
        }) {
          try {
            const { stdout, stderr } = await execFileAsync(
              "dotnet",
              ["run", "--project", cliProjectPath, "--", casesDir],
              {
                cwd: repoRoot,
                env: {
                  ...process.env,
                  RELEASETWIN_API_TOKEN: token,
                  RELEASETWIN_API_URL: apiUrl,
                  ...env,
                },
                maxBuffer: 10 * 1024 * 1024,
              },
            );
            return { code: 0, stdout, stderr };
          } catch (error) {
            const execError = error as { code?: number; stdout?: string; stderr?: string };
            return {
              code: execError.code ?? 1,
              stdout: execError.stdout ?? "",
              stderr: execError.stderr ?? String(error),
            };
          }
        },

        // hosted-journeys 5.9: same shape as runCli, but runs a hosted, pinned journey version
        // (`--journey <id>@<version>`) instead of a local cases directory — the CLI's hosted-fetch
        // path (RunJourneyAsync), exercised for real end to end.
        async runCliJourney({
          token,
          apiUrl,
          journeyRef,
        }: {
          token: string;
          apiUrl: string;
          journeyRef: string;
        }) {
          try {
            const { stdout, stderr } = await execFileAsync(
              "dotnet",
              ["run", "--project", cliProjectPath, "--", "--journey", journeyRef],
              {
                cwd: repoRoot,
                env: {
                  ...process.env,
                  RELEASETWIN_API_TOKEN: token,
                  RELEASETWIN_API_URL: apiUrl,
                  RELEASETWIN_FIXTURES_ROOT: path.join(repoRoot, "examples", "fixtures"),
                },
                maxBuffer: 10 * 1024 * 1024,
              },
            );
            return { code: 0, stdout, stderr };
          } catch (error) {
            const execError = error as { code?: number; stdout?: string; stderr?: string };
            return {
              code: execError.code ?? 1,
              stdout: execError.stdout ?? "",
              stderr: execError.stderr ?? String(error),
            };
          }
        },
      });

      return config;
    },
  },
});
