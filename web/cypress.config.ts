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
import { sendBillingWebhook } from "./scripts/e2e-billing.mjs";

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
  // ui-session-video: off by default (Cypress 15's own default) — CI and every e2e:* script are
  // unaffected. `demo:naha-video` sets CYPRESS_VIDEO=true to record the dashboard half of the flow.
  video: process.env.CYPRESS_VIDEO === "true",
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

        // naha-real-journey: real NAHA test-account credentials (the e2e login shared secret, the
        // deployed API's base URL, and an admin test-user email) live in AWS Secrets Manager, same
        // convention as fetchGitHubTestAccount/fetchLaunchDarklyTestAccount above. The secret itself
        // is stored as a ReleaseTwin project secret through the real dashboard form during the test
        // — this task only supplies the real values to put there.
        async fetchNahaTestAccount() {
          const client = new SecretsManagerClient({});
          const response = await client.send(
            new GetSecretValueCommand({ SecretId: "releasetwin/e2e/naha-account" }),
          );
          if (!response.SecretString) {
            throw new Error("releasetwin/e2e/naha-account has no SecretString value.");
          }

          const { e2eAuthSecret, apiBaseUrl, adminEmail } = JSON.parse(response.SecretString) as {
            e2eAuthSecret: string;
            apiBaseUrl: string;
            adminEmail: string;
          };

          return { e2eAuthSecret, apiBaseUrl, adminEmail };
        },

        // ui-journey-visual-evidence: the NAHA *admin UI* target — the deployed `e2e-admin` Vercel
        // alias plus the cookie its E2E-auth middleware gates on (naha_e2e_role=admin). Reuses the
        // same releasetwin/e2e/naha-account secret as fetchNahaTestAccount (adding two keys), so
        // there's one place for NAHA e2e config. Fails with a setup hint if the keys are absent.
        async fetchNahaAdminUiTarget() {
          const client = new SecretsManagerClient({});
          const response = await client.send(
            new GetSecretValueCommand({ SecretId: "releasetwin/e2e/naha-account" }),
          );
          if (!response.SecretString) {
            throw new Error("releasetwin/e2e/naha-account has no SecretString value.");
          }

          const parsed = JSON.parse(response.SecretString) as {
            e2eAuthSecret?: string;
            apiBaseUrl?: string;
            adminEmail?: string;
            adminUiBaseUrl?: string;
            roleCookieName?: string;
            roleCookieValue?: string;
          };

          const missing = (
            [
              ["adminUiBaseUrl", parsed.adminUiBaseUrl],
              ["roleCookieName", parsed.roleCookieName],
              ["roleCookieValue", parsed.roleCookieValue],
              ["apiBaseUrl", parsed.apiBaseUrl],
              ["e2eAuthSecret", parsed.e2eAuthSecret],
              ["adminEmail", parsed.adminEmail],
            ] as const
          )
            .filter(([, v]) => !v)
            .map(([k]) => k);

          if (missing.length > 0) {
            throw new Error(
              `releasetwin/e2e/naha-account is missing key(s) for the admin UI target: ${missing.join(", ")}. ` +
                "Add adminUiBaseUrl (the naha-admin e2e-admin Vercel alias), roleCookieName (naha_e2e_role), " +
                "and roleCookieValue (admin) to that secret.",
            );
          }

          return {
            adminUiBaseUrl: parsed.adminUiBaseUrl!.replace(/\/$/, ""),
            roleCookieName: parsed.roleCookieName!,
            roleCookieValue: parsed.roleCookieValue!,
            apiBaseUrl: parsed.apiBaseUrl!.replace(/\/$/, ""),
            e2eAuthSecret: parsed.e2eAuthSecret!,
            adminEmail: parsed.adminEmail!,
          };
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

        // flag-control-verify-ld-e2e 1.1: writes a throwaway flag-proof case whose `control` /
        // `control.verify` blocks drive LaunchDarkly's REST API directly (no `ld.*` adapter) —
        // toggling a real flag with a JSON Patch PATCH and reading it back. `${LD_API_TOKEN}` /
        // `${LD_PROJECT_KEY}` resolve from hosted project secrets; the environment key is baked in
        // literally, because JSONPath expressions (`json_path`, the Patch `path`) are not
        // env-interpolated. The pipeline reads the same flag back and asserts it is `on`, so the
        // known-bad leg (flag off) fails and the known-good leg (flag on) passes → deterministic
        // `Passed` regardless of the flag's prior value.
        async writeHttpFlagControlCase({
          directory,
          caseId,
          flagKey,
          environmentKey,
        }: {
          directory: string;
          caseId: string;
          flagKey: string;
          environmentKey: string;
        }) {
          const fs = await import("node:fs/promises");
          const casesDir = path.join(directory, "cases");
          const fixturesDir = path.join(directory, "fixtures");
          await fs.mkdir(casesDir, { recursive: true });
          await fs.mkdir(fixturesDir, { recursive: true });

          await fs.writeFile(path.join(fixturesDir, `${caseId}.json`), "{}\n");

          const flagUrl = `https://app.launchdarkly.com/api/v2/flags/\${LD_PROJECT_KEY}/{{featureKey}}`;
          const yaml = [
            `id: ${caseId}`,
            "oracle:",
            `  locator: tickets/${caseId}`,
            "fixture:",
            `  locator: ${caseId}.json`,
            "pipeline:",
            "  - operation: http.request",
            "    with:",
            "      method: GET",
            `      url: https://app.launchdarkly.com/api/v2/flags/\${LD_PROJECT_KEY}/${flagKey}`,
            "      headers:",
            "        Authorization: ${LD_API_TOKEN}",
            "  - operation: http.assertJsonPath",
            "    with:",
            `      path: $.environments.${environmentKey}.on`,
            "      expected: true",
            "flag_proof:",
            `  feature_key: ${flagKey}`,
            `  build_identity: ${caseId}-build`,
            "  control:",
            "    method: PATCH",
            `    url: ${flagUrl}`,
            "    headers:",
            "      Authorization: ${LD_API_TOKEN}",
            "      Content-Type: application/json",
            `    body: '[{"op":"replace","path":"/environments/${environmentKey}/on","value":{{enabled}}}]'`,
            "    verify:",
            "      method: GET",
            `      url: ${flagUrl}`,
            `      json_path: $.environments.${environmentKey}.on`,
            `      expected: "{{enabled}}"`,
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

        // billing-integration e2e: POST a Standard-Webhooks-signed Polar subscription event to the
        // local hosted API, exactly as Polar's servers would. This is how a spec moves a test org
        // between billing states — the real Polar hosted checkout can't be driven from Cypress, and
        // the webhook is the only writer of billing-driven tier/status anyway (design.md D2).
        async sendBillingWebhook(args: {
          orgId: string;
          type?: string;
          status?: string;
          subscriptionId?: string;
          customerId?: string;
          cadence?: string;
        }) {
          return sendBillingWebhook(args);
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
          env,
        }: {
          token: string;
          apiUrl: string;
          journeyRef: string;
          env?: Record<string, string>;
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
                  // ui-journey-visual-evidence: lets a spec turn on the UI adapter + evidence capture
                  // (RELEASETWIN_UI_ENABLED / RELEASETWIN_EVIDENCE), same passthrough runCli already has.
                  // ui-session-video: RELEASETWIN_UI_VIDEO_DIR flows through `...process.env` above —
                  // `demo:naha-video` sets it so the CLI records the NAHA-driving half of the flow.
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
      });

      return config;
    },
  },
});
