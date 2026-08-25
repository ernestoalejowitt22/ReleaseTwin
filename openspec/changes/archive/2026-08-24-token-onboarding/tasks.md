## 1. Preparation

- [x] 1.1 Read the relevant guide(s) under `web/node_modules/next/dist/docs/` before touching `web/` code — this repo's Next.js version has breaking changes vs. training data (`web/AGENTS.md`). Confirmed the existing `"use client"` + `useState` + server-action pattern already used in `issue-token-button.tsx` is current; no API changes affect this additive change.

## 2. Instructions component

- [x] 2.1 Add a static install/run instructions block (module-level constant or inline JSX) to `web/src/app/dashboard/issue-token-button.tsx`, rendered alongside the token when `token` is set.
- [x] 2.2 Include the exact `export RELEASETWIN_API_TOKEN=<token>` line, interpolating the real issued token value.
- [x] 2.3 Include the `dotnet run --project src/ReleaseTwin.Cli -- examples/cases` invocation.
- [x] 2.4 Include the optionality statement: running without the token stays local/free; setting it links runs to this project.

## 3. Verification

- [x] 3.1 Verified via a real end-to-end run, not just local inspection: extended the existing `web/cypress/e2e/dashboard-walkthrough.cy.ts` (real Clerk sign-in, real hosted API, real dashboard) with assertions for the new instructions text, and ran it live — signed in, created a project, issued a token, and confirmed the token, the `export`/`dotnet run` block, and the optionality text all rendered together (screenshot: `cypress/screenshots/.../03-token-issued.png`).
- [x] 3.2 Confirm the displayed `export`/`dotnet run` commands are copy-paste-accurate against the CLI's actual documented invocation in `README.md`.
- [x] 3.3 Confirm existing token-issuance behavior (token shown once, "copy it now" warning styling) is unchanged — this change is additive only. Verified by the same e2e run: pre-existing assertions (token label, `rtw_` prefix) still passed unmodified alongside the new ones.

## 4. Follow-up (not part of this change)

- [ ] 4.1 (Deferred) Once a real version is tagged and published via `cli-packaging`'s release workflow, swap the shown command to the Docker `docker pull`/`docker run` form in a small follow-up change.
