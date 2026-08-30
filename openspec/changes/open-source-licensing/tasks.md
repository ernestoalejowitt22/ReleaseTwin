## 1. Licenses

- [x] 1.1 `LICENSE` — **AGPL-3.0** full text (revised from Apache-2.0 — see proposal note).
- [x] 1.1a `LICENSE.EXCEPTIONS` — Adapter Linking Exception (AGPL §7 additional permission) so
      independent adapters can be released under any OSI/proprietary license.
- [x] 1.1b `examples/LICENSE` — Apache-2.0 (scaffold output must stay permissive).
- [x] 1.2 `hosted/LICENSE` — BSL 1.1 with parameters filled (Licensor, Licensed Work = `hosted/` +
      `web/`, Additional Use Grant, Change Date = 4y/version, Change License = Apache-2.0).
- [x] 1.3 `web/LICENSE` — identical copy of `hosted/LICENSE`.
- [x] 1.4 `LICENSING.md` — path→license table (AGPL engine + exception / Apache examples / BSL
      hosted), plain-English summaries, rationale, contribution + trademark stance.

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
- [x] 3.2 `.github/workflows/secret-scan.yml` (PR #20, merged) — runs the `gitleaks` binary
      directly (`gitleaks git` over full history) on every PR/push, a weekly cron, and
      `workflow_dispatch`. `.gitleaks.toml` extends the default ruleset, allowlisting build
      output / deps / `.env*` and the deliberately-fake example+test fixture credentials.
      Validated via Docker: 68 commits, **0 leaks**. No GitHub-API perms needed (the earlier
      `gitleaks-action` approach hit a 403 and was dropped).

## 4. Validation

- [x] 4.1 `openspec validate open-source-licensing --strict` passes (`skip_specs: true`).
- [x] 4.2 `dotnet build ReleaseTwin.sln` unaffected (no source touched) — not re-run; governance
      files only.

## 5. Out of scope (operator / follow-up)

- [ ] 5.1 Flip the repo public; add topics, description, a README GIF; link "Create a free
      account" once the funnel is ready (Workstream D).
- [ ] 5.2 SPDX headers on every source file (mechanical sweep).
- [ ] 5.3 Legal review of the BSL parameters + an Apache `NOTICE` file if wanted.
