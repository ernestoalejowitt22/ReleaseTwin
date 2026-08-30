## Why

The repo has **no license file at all**, which legally means all-rights-reserved — nobody can
use, fork, or run it. The go-to-market model in `docs/go-to-market.md` is open core, and
`docs/self-serve-funnel-plan.md` names this Workstream A: "small, do first — it unblocks the
whole open-core narrative." Rungs 0–2 of the funnel (run `docker run` against your own API, write
your own case, wire into CI) all assume the engine is openly licensed.

## What Changes

Repo governance only — no code, no behavior change. `.openspec.yaml` sets `skip_specs: true`.

- **`LICENSE`** — Apache-2.0, covering the engine: `src/`, `tests/`, `examples/`, `docs/`, and
  the repo-root build glue.
- **`hosted/LICENSE` + `web/LICENSE`** — Business Source License 1.1 for the commercial surface.
  Licensor: Ernesto Alejo. Change License: Apache-2.0. Change Date: 4 years per published
  version. Additional Use Grant: everything except reselling it as a competing hosted/managed
  commercial service. This implements the funnel plan's recommendation ("BSL in-repo, simplest to
  operate solo") rather than splitting `hosted/` into a private repo.
- **`LICENSING.md`** — the map: which paths are Apache-2.0, which are BSL, why, and the
  contribution + trademark stance.
- **`CONTRIBUTING.md`** — issue-first workflow, DCO sign-off, per-path contribution licensing,
  build commands, the OpenSpec expectation.
- **`SECURITY.md`** — private reporting (GitHub advisory + email), scope (false verdicts, evidence
  redaction bypass, hosted authz/tenant isolation), pre-1.0 support policy.
- **`.github/`** — issue templates (bug, feature) + `config.yml` routing security/discussion, and
  a PR template with the per-path license checkbox and the no-secrets check.

## Out of scope (tracked, not done here)

- **Flipping the repo public**, topics, description, README GIF — an operator action once this
  merges.
- **A CI secret-scanning job** (gitleaks/trufflehog). A manual sweep of all 77 commits for AWS
  keys, Clerk/Stripe secrets, GitHub PATs, private-key blocks, Slack/GCP tokens, and committed
  `.env` files found **nothing**, but an automated gate should be added before/with going public.
- **SPDX headers** on every source file — a mechanical follow-up sweep.
- **Legal review** of the BSL parameters and the Apache NOTICE file.

## Impact

- New top-level files: `LICENSE`, `LICENSING.md`, `CONTRIBUTING.md`, `SECURITY.md`.
- New: `hosted/LICENSE`, `web/LICENSE`, `.github/ISSUE_TEMPLATE/*`, `.github/PULL_REQUEST_TEMPLATE.md`.
- No source, test, build, or CI change. `ReleaseTwin.sln` untouched.
