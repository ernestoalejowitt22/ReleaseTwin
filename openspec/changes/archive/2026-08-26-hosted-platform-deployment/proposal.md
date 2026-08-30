## Why

Everything the hosted platform can do has only ever been exercised locally — `npm run dev`, `dotnet run`, DynamoDB Local. There is no real, reachable instance of ReleaseTwin anywhere; "AWS" so far means only a `terraform` file for one DynamoDB table that has never been applied. Before any design-partner conversation can happen, there needs to be something the operator (currently the only intended user — Ernesto, acting as his own first customer) can actually sign up on, connect a real GitHub account to, and run the CLI against, over the real internet, not `localhost`.

## What Changes

- Apply the existing DynamoDB table terraform (`hosted/terraform/main.tf`) against a real AWS account — the the operator’s own account, not that separate shared account.
- Deploy `ReleaseTwin.Hosted.Api` to AWS Lambda (managed .NET 10 runtime, confirmed available) behind a Lambda Function URL — chosen over App Runner/ECS specifically for near-zero idle cost (pay-per-invocation, free tier), accepting cold-start latency as a known tradeoff. No custom domain, no ACM, no ALB — the Function URL's own AWS-issued HTTPS address is the API's public endpoint.
- Deploy `web/` to Vercel, using its default `*.vercel.app` domain — no custom domain for now.
- Keep Clerk on its existing dev instance (`<slug>.clerk.accounts.dev`, same keys already in `web/.env.local`) — no new Clerk application, no production Clerk migration.
- Register a real GitHub OAuth App (distinct from Clerk) so "Connect GitHub" genuinely works, with its callback URL pointed at the deployed Vercel URL — this is being done because the operator will actually use this instance as a real customer would, not left unconfigured as it is locally.
- Wire the deployed API's required runtime configuration that local dev currently sets ad hoc via shell exports (`Clerk__Domain`, `Aws__DynamoDb__*`/`Aws__Region`, `GitHubConnection__*`) as real Lambda environment variables/secrets.

## Capabilities

(none — this stands up real infrastructure for behavior that already exists and is already specified elsewhere; no product requirements change. `skip_specs: true` set in `.openspec.yaml`.)

## Impact

- New: `hosted/ReleaseTwin.Hosted.Api`'s entry point gains AWS Lambda hosting support (`Amazon.Lambda.AspNetCoreServer.Hosting`), additive to the existing Kestrel-hosted `dotnet run` path used by local dev and `web/`'s e2e suite — both must keep working.
- New: DynamoDB table actually provisioned in AWS (the operator’s account account), via the existing terraform.
- New: a Vercel project for `web/`, with its own environment variables (`RELEASETWIN_API_URL` pointing at the deployed Function URL, Clerk keys, `GitHubConnection` client ID only if the frontend needs it — verify during design).
- New: a registered GitHub OAuth App, distinct from any Clerk-side OAuth configuration.
- No changes to `ReleaseTwin.Core`, the CLI's own behavior, or any hosted API request/response contract — this is deployment topology only.
- Costs real (if small) money for the first time — everything before this was either free-tier local tooling or unused terraform.
