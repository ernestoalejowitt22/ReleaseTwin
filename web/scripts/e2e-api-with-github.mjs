import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { config as loadDotenv } from "dotenv";
import { SecretsManagerClient, GetSecretValueCommand } from "@aws-sdk/client-secrets-manager";

// e2e-github-connection-flow design.md: the second, localhost-only GitHub OAuth App's Client
// ID/Secret/CallbackUrl live in AWS Secrets Manager too, alongside the test account's own
// credentials — fetched here (using whatever AWS credentials are already configured in this
// shell) and passed to `dotnet run` as env vars, the same names Program.cs already reads
// (`GitHubConnection:ClientId` etc. via ASP.NET Core's `__`-as-`:` env var convention). Nobody
// needs to export these by hand, and nobody outside this process ever sees the raw values.
const webDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const hostedDir = path.join(webDir, "..", "hosted");

loadDotenv({ path: path.join(webDir, ".env.local") });
const clerkDomain = process.env.CLERK_DOMAIN?.trim();
if (!clerkDomain) {
  throw new Error(
    "CLERK_DOMAIN is not set. Add it to web/.env.local (see web/.env.local.example).",
  );
}

const client = new SecretsManagerClient({});
const response = await client.send(
  new GetSecretValueCommand({ SecretId: "releasetwin/e2e/github-oauth-app" }),
);
if (!response.SecretString) {
  throw new Error("releasetwin/e2e/github-oauth-app has no SecretString value.");
}

const { clientId, clientSecret, callbackUrl } = JSON.parse(response.SecretString);

const child = spawn(
  "dotnet",
  ["run", "--project", "ReleaseTwin.Hosted.Api", "--urls", "http://localhost:5199"],
  {
    cwd: hostedDir,
    stdio: "inherit",
    env: {
      ...process.env,
      Clerk__Domain: clerkDomain,
      GitHubConnection__ClientId: clientId,
      GitHubConnection__ClientSecret: clientSecret,
      GitHubConnection__CallbackUrl: callbackUrl,
    },
  },
);

child.on("exit", (code) => process.exit(code ?? 1));
