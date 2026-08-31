import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { config as loadDotenv } from "dotenv";
import { E2E_POLAR_ENV } from "./e2e-billing.mjs";

// Local e2e helper: starts ReleaseTwin.Hosted.Api for the Cypress specs. The Clerk Frontend API
// domain (the JWT issuer the API validates against) is not hardcoded — it comes from CLERK_DOMAIN
// in web/.env.local, the same file cypress.config.ts and `next dev` read. See .env.local.example.
const webDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const hostedDir = path.join(webDir, "..", "hosted");

loadDotenv({ path: path.join(webDir, ".env.local") });

const clerkDomain = process.env.CLERK_DOMAIN?.trim();
if (!clerkDomain) {
  console.error(
    "CLERK_DOMAIN is not set. Add it to web/.env.local (see web/.env.local.example) — " +
      "it's your Clerk instance's Frontend API domain, e.g. <slug>.clerk.accounts.dev.",
  );
  process.exit(1);
}

const child = spawn(
  "dotnet",
  ["run", "--project", "ReleaseTwin.Hosted.Api", "--urls", "http://localhost:5199"],
  {
    cwd: hostedDir,
    stdio: "inherit",
    // E2E_POLAR_ENV makes the billing surface live (signed webhook accepted, upgrade button shown)
    // so billing.cy.ts and the paid-tier setup in other specs work without touching real Polar.
    env: { ...process.env, Clerk__Domain: clerkDomain, ...E2E_POLAR_ENV },
  },
);

child.on("exit", (code) => process.exit(code ?? 0));
