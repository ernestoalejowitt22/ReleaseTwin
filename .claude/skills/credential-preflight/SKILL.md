---
name: credential-preflight
description: Enumerate every credential, IAM permission, OAuth scope, and GitHub Actions secret-vs-variable a task needs, with a verify command for each, before work starts. Use before infra, deploy, or external-service tasks.
allowed-tools: Bash(gh:*), Bash(aws:*), Bash(grep:*), Bash(git:*)
metadata:
  author: ernestoalejo
  version: "1.0"
---

Surface the whole permission surface up front so nothing stalls a long run.

**Steps**

1. From the task description and the code it touches, list every external
   dependency: AWS services + specific IAM actions, OAuth scopes (gh, Clerk,
   LaunchDarkly), GitHub Actions secrets and variables.
2. Parse `.github/workflows/*.yml` for every `${{ secrets.* }}` and `${{ vars.* }}`
   reference the task's path exercises. Cross-check against `gh secret list` and
   `gh variable list` (repo and each environment). Flag name mismatches and
   type mismatches (stored as Secret, read as `vars.`, or vice versa).
3. For `gh`: confirm scopes with `gh auth status`. The `workflow` scope is
   required for anything touching `.github/workflows/`.
4. For AWS: name the exact API calls needed (e.g. `secretsmanager:PutSecretValue`)
   and give a single read-only command to check access for each.
5. Output a table: item | how to verify now (one command) | status / gap.
6. List the exact `.claude/settings.local.json` allowlist entries to add so the
   task runs without permission interruptions. Wait for the user to close gaps
   before starting.
