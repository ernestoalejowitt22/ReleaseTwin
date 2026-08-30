## 1. Licenses

- [x] 1.1 `LICENSE` — Apache-2.0 full text, copyright line "2026 Ernesto Alejo and the ReleaseTwin
      contributors".
- [x] 1.2 `hosted/LICENSE` — BSL 1.1 with parameters filled (Licensor, Licensed Work = `hosted/` +
      `web/`, Additional Use Grant, Change Date = 4y/version, Change License = Apache-2.0).
- [x] 1.3 `web/LICENSE` — identical copy of `hosted/LICENSE`.
- [x] 1.4 `LICENSING.md` — path→license table, plain-English BSL summary, rationale, contribution
      + trademark stance.

## 2. Governance files

- [x] 2.1 `CONTRIBUTING.md` — issue-first, one-change-per-PR, DCO sign-off, per-path contribution
      licensing, engine + hosted build commands, OpenSpec expectation.
- [x] 2.2 `SECURITY.md` — private reporting channels, in/out-of-scope, secrets/evidence handling
      note, pre-1.0 "latest main only" support.
- [x] 2.3 `.github/ISSUE_TEMPLATE/bug_report.md`, `feature_request.md`, `config.yml`
      (blank issues off; security → advisory, questions → discussions).
- [x] 2.4 `.github/PULL_REQUEST_TEMPLATE.md` — per-path license checkbox, DCO + no-secrets checks.

## 3. Secret history sweep

- [x] 3.1 Sweep all 77 commits for AWS keys (`AKIA…`), Clerk/Stripe secrets (`sk_live/test…`),
      GitHub PATs (`ghp_…`), private-key blocks, Slack (`xox…`) / GCP (`AIza…`) tokens, and any
      committed `.env` file. **Result: clean — no matches.**
- [ ] 3.2 Add a CI secret-scanning job (gitleaks or trufflehog) — deferred to its own change;
      neither tool is installed locally. Must land before the repo goes public.

## 4. Validation

- [x] 4.1 `openspec validate open-source-licensing --strict` passes (`skip_specs: true`).
- [x] 4.2 `dotnet build ReleaseTwin.sln` unaffected (no source touched) — not re-run; governance
      files only.

## 5. Out of scope (operator / follow-up)

- [ ] 5.1 Flip the repo public; add topics, description, a README GIF; link "Create a free
      account" once the funnel is ready (Workstream D).
- [ ] 5.2 SPDX headers on every source file (mechanical sweep).
- [ ] 5.3 Legal review of the BSL parameters + an Apache `NOTICE` file if wanted.
