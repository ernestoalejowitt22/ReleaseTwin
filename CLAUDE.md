# ReleaseTwin — working agreement

## Before proposing manual steps
Always look for a code-side fix first. If a task seems to need manual user action
(setting Vercel env vars, running Terraform locally, hand-editing
`.claude/settings.local.json`, clicking through an AWS/Clerk console), propose the
automated/code alternative first. Fall back to a manual step only when no code path
exists, and then state explicitly why it is unavoidable and list it separately at
the end of your report.

## Scope checkpoint before big implementations
For any non-trivial change, before writing code produce a one-page scope proposal:
(a) what is in Phase 1, (b) what is explicitly deferred, (c) the data-model /
architecture decision and the one alternative rejected, (d) any manual steps the
user would have to do. Wait for approval before touching files. The OpenSpec
proposal step satisfies this — don't jump past it into implementation.

## Architecture conventions
- **DynamoDB:** single-table design by default (PK/SK with overloaded GSIs). Do
  not propose multi-table layouts without asking first.
- **Deployment:** CI-only. Terraform runs in GitHub Actions via OIDC, never a
  local `terraform apply`. Keep the split-layer structure.
- Prefer code-side automation over standing manual configuration.

## Credential / permission pre-flight
Before a task that touches infra or external services, enumerate every credential,
IAM permission, OAuth scope, and GitHub Actions secret-vs-variable it will need,
with a one-line command to verify each. Flag gaps before starting so they can be
fixed (and pre-approved in settings) in one pass rather than mid-run.

## Git & repo conventions
- Repo: `github.com/ernestoalejowitt22/ReleaseTwin`. Author email is set via a
  global `includeIf` rule — don't override it per-repo.
- Ensure `gh auth` has the `workflow` scope before pushing anything that touches
  `.github/workflows/`.
- Never commit real Clerk/AWS/LaunchDarkly secrets; verify `.gitignore` covers
  `.env*` and credential files before a first commit.
- Commit/push only when asked. Branch first if on `main`.

## Answering scope
When asked to document or diagram a flow, cover the complete path including
signup/onboarding and edge cases — read the code, not just existing docs. When a
request is ambiguous, restate it and ask one clarifying question before producing
a long answer or writing code.

## Verification
- **Web (`web/`):** `npm run build` (next build) + `npx eslint`.
- **.NET:** `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln`.
- **OpenSpec:** `openspec validate <change> --strict`.
Run the relevant set before reporting a change complete. Report actual test
counts and any deferred tasks. If a design element proves technically infeasible
mid-implementation (e.g. a tool writes to a fixed path), stop and flag it rather
than silently redesigning.

## Evidence quality
ReleaseTwin is a test-evidence product — the artifact *is* the deliverable. After
a test run, open the generated artifacts (videos, screenshots, evidence folder),
report the exact path they landed in vs. what the design specified, and describe
what is actually visible. Call out blank frames, spinner-only clips, or
wrong-location output explicitly.

## OpenSpec state
Open (non-archived) changes live in `openspec/changes/`. Don't archive a change
until the user confirms. Tasks marked **"Needs the user to run this"** are blocked
on real AWS/infra access — leave them unchecked and surface them.
