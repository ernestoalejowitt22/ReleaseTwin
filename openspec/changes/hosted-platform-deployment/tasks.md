## 1. Lambda hosting code change (local, no AWS yet)

- [x] 1.1 Add the `Amazon.Lambda.AspNetCoreServer.Hosting` NuGet package to `ReleaseTwin.Hosted.Api.csproj`.
- [x] 1.2 Add `builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);` to `Program.cs`; confirm `dotnet run --project ReleaseTwin.Hosted.Api` and `npm run e2e` (from `web/`) still work unmodified afterward.
- [x] 1.3 Add `Amazon.Lambda.Tools` (`dotnet tool install -g Amazon.Lambda.Tools` if not already present) and whatever `aws-lambda-tools-defaults.json` config its `package` (build-only) subcommand needs. (No `aws-lambda-tools-defaults.json` needed — `dotnet lambda package` inferred everything from the csproj.)
- [x] 1.4 `dotnet lambda package` from `ReleaseTwin.Hosted.Api` — confirm it produces a deployment zip without touching AWS. (5.6MB zip, 208 files, produced with zero AWS calls.)

## 2. Main terraform config (`hosted/terraform`) — DynamoDB + Lambda + IAM role + Function URL

- [x] 2.1 Extend `hosted/terraform` with: `aws_lambda_function` (pointed at the zip from 1.4), a least-privilege `aws_iam_role`/`aws_iam_role_policy` (scoped to `dynamodb:GetItem/PutItem/UpdateItem/Query` etc. on the table + its two GSIs, plus basic Lambda execution/CloudWatch Logs permissions), and `aws_lambda_function_url` (`AuthType: NONE`) — alongside the existing `aws_dynamodb_table` resource. Add variables for `GitHubConnection__ClientId/ClientSecret/CallbackUrl` (defaulted to empty strings) and `Clerk__Domain`, wired into the Lambda's environment block. (New `hosted/terraform/lambda.tf`; also had to bump `main.tf`'s AWS provider constraint from `~> 5.0` to `~> 6.0` — the `dotnet10` runtime identifier isn't recognized by the provider's schema until 6.x, confirmed empirically. Safe: no state has ever been applied under 5.x. `terraform validate` and a full `terraform plan` both pass clean — 6 resources to add, 0 errors.)
- [x] 2.2 Add an `s3` backend block to `hosted/terraform/main.tf` pointing at the (not-yet-existing) bucket/table from group 3 — deterministic names, known in advance, so this can be written before those resources exist.

## 3. Bootstrap terraform — `hosted/terraform-state-backend` (S3 + DynamoDB lock, local state) and `hosted/terraform-bootstrap` (OIDC provider + CI role, remote state)

- [x] 3.1 New `hosted/terraform-state-backend`: `aws_s3_bucket` (`releasetwin-terraform-state-846136340491`, versioned, public access blocked) + `aws_dynamodb_table` for locking. No backend block — this is the one layer that stays genuinely local-stated (see design.md Decisions/Risks for why that's accepted here specifically). `terraform validate` and `terraform plan` both pass clean — 4 resources.
- [x] 3.2 New `hosted/terraform-bootstrap`: `aws_iam_openid_connect_provider` for `token.actions.githubusercontent.com`; an `aws_iam_role` trusted only for `repo:ernestoalejowitt22/ReleaseTwin:*` via that provider; a least-privilege policy scoped to exactly what `hosted/terraform`'s `apply` needs (DynamoDB table, Lambda function, the Lambda execution role + `iam:PassRole`, plus S3/DynamoDB access to the state backend from 3.1). Backend block wired to the bucket/table from 3.1 (durable state from its first apply, unlike 3.1 itself). `terraform validate` passes clean.

## 4. GitHub Actions workflows

- [x] 4.1 New `.github/workflows/bootstrap.yml`: `workflow_dispatch`-triggered, two sequential jobs (`state-backend` applying group 3.1, `oidc-and-role` applying group 3.2), both authenticated via a short-lived AWS session read from `AWS_BOOTSTRAP_ACCESS_KEY_ID`/`AWS_BOOTSTRAP_SECRET_ACCESS_KEY`/`AWS_BOOTSTRAP_SESSION_TOKEN` repo secrets (pasted from a locally-minted MFA session — this can't use OIDC, since it's what creates the OIDC trust). YAML validated.
- [x] 4.2 New `.github/workflows/deploy-hosted.yml`: `workflow_dispatch`-triggered (not on push — design.md Non-Goals), assumes the CI role via OIDC (`aws-actions/configure-aws-credentials`, no stored AWS secret), builds the Lambda zip (`dotnet lambda package`), runs `terraform apply` for `hosted/terraform` with `table_prefix`/`region` as workflow inputs and `GitHubConnection__*` sourced from repo secrets/variables. `permissions: id-token: write` set for OIDC token issuance. YAML validated.

## 5. Create the `releasetwin-bootstrap` IAM user (the one MFA'd action in this whole plan — cannot be done by an agent)

- [ ] 5.1 In the AWS Console (`ealejo` account, logged in normally — MFA here is just your regular login, not a CLI session dance): IAM → Users → Create user `releasetwin-bootstrap`, no console access, access-key-only credential type.
- [ ] 5.2 Attach an inline policy with exactly this — S3/DynamoDB for the state backend, IAM/OIDC-provider actions for the trust and role, nothing else:
  ```json
  {
    "Version": "2012-10-17",
    "Statement": [
      {
        "Sid": "StateBucket",
        "Effect": "Allow",
        "Action": [
          "s3:CreateBucket", "s3:GetBucketLocation", "s3:GetBucketVersioning",
          "s3:PutBucketVersioning", "s3:GetBucketPublicAccessBlock",
          "s3:PutBucketPublicAccessBlock", "s3:GetEncryptionConfiguration",
          "s3:GetBucketAcl", "s3:ListBucket", "s3:GetObject", "s3:PutObject"
        ],
        "Resource": [
          "arn:aws:s3:::releasetwin-terraform-state-846136340491",
          "arn:aws:s3:::releasetwin-terraform-state-846136340491/*"
        ]
      },
      {
        "Sid": "StateLockTable",
        "Effect": "Allow",
        "Action": [
          "dynamodb:CreateTable", "dynamodb:DescribeTable",
          "dynamodb:GetItem", "dynamodb:PutItem", "dynamodb:DeleteItem"
        ],
        "Resource": "arn:aws:dynamodb:us-east-1:846136340491:table/releasetwin-terraform-state-lock"
      },
      {
        "Sid": "OidcAndRole",
        "Effect": "Allow",
        "Action": [
          "iam:CreateOpenIDConnectProvider", "iam:GetOpenIDConnectProvider",
          "iam:ListOpenIDConnectProviders", "iam:TagOpenIDConnectProvider",
          "iam:CreateRole", "iam:GetRole", "iam:PutRolePolicy",
          "iam:GetRolePolicy", "iam:DeleteRolePolicy", "iam:TagRole"
        ],
        "Resource": "*"
      }
    ]
  }
  ```
- [ ] 5.3 Generate an access key for the user; add its `AccessKeyId`/`SecretAccessKey` as the `AWS_BOOTSTRAP_ACCESS_KEY_ID`/`AWS_BOOTSTRAP_SECRET_ACCESS_KEY` repo secrets (standing, kept permanently — GitHub secrets are encrypted at rest and never re-displayed; see design.md Decisions).
- [ ] 5.4 Trigger `bootstrap.yml` (`gh workflow run bootstrap.yml` or the Actions UI). Confirm both jobs succeed; capture the CI role ARN from the `oidc-and-role` job's summary.
- [ ] 5.5 Set the CI role ARN from 5.4 as the `AWS_DEPLOY_ROLE_ARN` repo variable (used by `deploy-hosted.yml`).

## 6. Terraform pass 1, via CI

- [ ] 6.1 Trigger `deploy-hosted.yml` with `table_prefix=releasetwin-dev-`, `region=us-east-1` (GitHub vars left at empty defaults for this first pass).
- [ ] 6.2 Confirm the run succeeds; capture the `function_url` output from the run's summary.
- [ ] 6.3 Smoke-test the Function URL directly (e.g. `curl <url>/Privacy` should return 200; `curl <url>/api/dashboard` should return 401, matching local behavior).

## 7. `web/` on Vercel

- [ ] 7.1 Create a new Vercel project pointed at `web/` (or the repo, with `web/` as the root directory).
- [ ] 7.2 Set Vercel environment variables: `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`, `CLERK_SECRET_KEY` (same values as `web/.env.local`), `RELEASETWIN_API_URL` = the Function URL from 6.2.
- [ ] 7.3 Deploy; confirm the app loads on its default `*.vercel.app` domain and `/sign-in` renders (Clerk widget reachable).

## 8. GitHub OAuth App + terraform pass 2, via CI

- [ ] 8.1 Register a new GitHub OAuth App at github.com/settings/developers — authorization callback URL `<vercel-url-from-7.3>/connect/github/callback`, no scope beyond the default (the flow itself requests `read:user` — verify against `GitHubConnectionFlowService`, don't request `repo`).
- [ ] 8.2 Store the real `GitHubConnection` Client ID/CallbackUrl as repo variables and the Client Secret as a repo secret (never committed).
- [ ] 8.3 Re-trigger `deploy-hosted.yml` — updates the Lambda's environment variables in place with the real GitHub values.

## 9. End-to-end verification (real usage, not automated)

- [ ] 9.1 Sign up for real through the deployed Vercel URL with a real email (not a `+clerk_test@` throwaway) — confirm landing on a freshly-provisioned dashboard.
- [ ] 9.2 Create a project; click "Connect GitHub"; authorize with a real GitHub account; confirm the connection shows on the dashboard.
- [ ] 9.3 Issue a token from the deployed dashboard.
- [ ] 9.4 From a local machine, `export RELEASETWIN_API_TOKEN=<issued token>` and `RELEASETWIN_API_URL=<Function URL>`, then `dotnet run --project src/ReleaseTwin.Cli -- examples/cases`.
- [ ] 9.5 Reload the deployed dashboard; confirm the run history and usage counters reflect the real upload (same `HTTP-DEMO-1` PASS / `CLM-042` FAIL shape already documented in the README, since Azure DevOps credentials still aren't configured for this instance).
