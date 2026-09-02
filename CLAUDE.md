# ReleaseTwin (engine) — working agreement

This repo is the AGPL engine, adapters, CLI, and their specs. The hosted platform
is a separate private repo.

## Scope checkpoint before big implementations
For any non-trivial change, before writing code produce a one-page scope proposal:
(a) what is in Phase 1, (b) what is explicitly deferred, (c) the
architecture decision and the one alternative rejected, (d) any manual steps the
user would have to do. Wait for approval before touching files. The OpenSpec
proposal step satisfies this — don't jump past it into implementation.

## Architecture conventions
- The core/adapter boundary is the invariant: an adapter plugs into
  `ReleaseTwin.AdapterSdk` / `ReleaseTwin.Core` extension points **without**
  modifying the core model or runner. A change that needs a core edit to land an
  adapter is a design smell — stop and flag it.
- Prefer code-side automation over standing manual configuration.

## Git & repo conventions
- Repo: `github.com/ernestoalejowitt22/ReleaseTwin`. Author email is set via a
  global `includeIf` rule — don't override it per-repo.
- Ensure `gh auth` has the `workflow` scope before pushing anything that touches
  `.github/workflows/`.
- Never commit real credentials; verify `.gitignore` covers `.env*` before a
  first commit.
- Commit/push only when asked. Branch first if on `main`.

## Answering scope
When asked to document or diagram a flow, cover the complete path and edge cases
— read the code, not just existing docs. When a request is ambiguous, restate it
and ask one clarifying question before producing a long answer or writing code.

## Verification
- **.NET:** `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln`.
- **The Action:** `node --test integrations/github-action/`.
- **OpenSpec:** `openspec validate <change> --strict`.
Run the relevant set before reporting a change complete. Report actual test
counts and any deferred tasks.

## Evidence quality
ReleaseTwin is a test-evidence product — the artifact *is* the deliverable. After
a test run, open the generated artifacts (videos, screenshots, evidence folder),
report the exact path they landed in vs. what the design specified, and describe
what is actually visible. Call out blank frames, spinner-only clips, or
wrong-location output explicitly.

## OpenSpec state
Active changes live in `openspec/changes/`. Don't archive a change until the user
confirms.
