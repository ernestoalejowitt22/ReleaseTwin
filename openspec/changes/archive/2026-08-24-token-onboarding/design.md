## Context

See `proposal.md` - Why. Relevant existing shape:

- `IssueTokenButton` (`web/src/app/dashboard/issue-token-button.tsx:12-33`) is the only place the raw token is ever held client-side; today it renders just the token string with "copy it now" — no instructions.
- `issueToken` (`web/src/app/dashboard/actions.ts:14-18`) POSTs to `/api/dashboard/projects/${projectId}/tokens` and returns `{ token }` only — no project name or other context comes back.
- `cli-packaging` shipped the Docker distribution path (`docker pull ghcr.io/ernestoalejowitt22/releasetwin/cli:<version>`), but no version has actually been tagged and published yet (its task 4.2 — pushing a real release tag — was deliberately deferred). The Docker command is not runnable today.
- `web/AGENTS.md` flags that this repo's Next.js version has breaking changes vs. training data and directs reading `node_modules/next/dist/docs/` before writing code here — a task-list item, not a design decision, but noted so it isn't skipped at apply time.

## Goals / Non-Goals

**Goals:**
- A customer sees a real, copy-paste-runnable command immediately next to a newly issued token.
- The instructions make the free/local vs. linked-to-project distinction explicit, not just implicit in behavior.

**Non-Goals:**
- Showing the Docker install command — deferred until a real version is actually tagged and published (see Decisions).
- Any backend/API changes — `issueToken`'s existing `{ token }` response is sufficient; no new data is needed to render the instructions.
- Per-project customization of the shown command (e.g. embedding the project's own case-file path) — the instructions point at the bundled zero-credential example, not a customer-specific path.

## Decisions

**Show the source-build command now, not Docker.** Per explicit decision: `dotnet run --project src/ReleaseTwin.Cli -- <cases-dir>` is shown today because it is real and works right now; the Docker command is deferred to a small follow-up change once a real version tag exists to point at (closing out `cli-packaging`'s deferred 4.2 first makes that follow-up trivial — swap one code block, no new design). This avoids shipping a command that would fail if a customer actually ran it.

**No new backend data needed.** The instructions reference the token value (already returned by `issueToken`) and a fixed, bundled example path (`examples/cases`) — nothing project-specific. `actions.ts` and the ingest/dashboard API are unchanged.

**Instructions render entirely client-side in `IssueTokenButton`.** The component already holds the token in local state after issuance; the install/run text is static (module-level constant), not derived from any new server data. This keeps the change frontend-only, matching the proposal's stated impact.

**Content shown alongside the token:**
```
New token (shown once, copy it now):
<token>

Set it and run a first case:

  export RELEASETWIN_API_TOKEN=<token>
  dotnet run --project src/ReleaseTwin.Cli -- examples/cases

This runs the bundled zero-credential example and uploads the result here.
Skipping this step keeps everything fully local and free — the token is
only what links a run to this project.
```
This satisfies both spec requirements in one block: the runnable command sequence, and the optionality statement, without a separate UI element for each.

## Risks / Trade-offs

- [The shown command still requires cloning/building from source, not a one-liner] → Accepted; this is the same friction that exists everywhere else pre-`cli-packaging`-release, and is exactly what the Docker follow-up removes once there's a real version to point at.
- [Text becomes stale once Docker is the real recommended path] → Low cost to fix (one static block in one file); tracked explicitly as the deferred follow-up, not left implicit.

## Open Questions

(none — the source-build-vs-Docker question was resolved before writing this design)
