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
      account" once the funnel is ready (Workstream D). Pre-flip audit done — see below.
- [x] 5.2 Per-file licensing declared via `REUSE.toml` + `LICENSES/` (REUSE 3.3, `reuse lint`
      green, 646/646 files) instead of a per-file header sweep. New files should still add an
      `SPDX-License-Identifier` header; the toml is the backstop.
- [ ] 5.3 Legal review — see the checklist in PR #22's description (the AGPL §7 exception, the
      BSL Additional Use Grant, licensor entity, DCO vs CLA). Add an Apache `NOTICE` if wanted.
- [x] 5.4 Pre-public secret/exposure audit: gitleaks CI clean (#20); NAHA is the operator's own
      project (OK to publish, and NAHA repo will be made public too); third-party account IDs and
      project names scrubbed from the archived hosted-deployment change docs; all references to a
      prior internal suite removed from the tree and purged from history (a fit-check doc and the
      `phase1-core-extraction` archived change deleted; the Clerk dev-instance slug de-hardcoded
      in #24). The operator's own AWS account ID remains in the terraform bucket
      names — not sensitive per AWS, and the backend block can't be parameterized. Move
      `docs/go-to-market.md` + `docs/self-serve-funnel-plan.md` out of the public tree before
      flipping (strategy/pricing detail).
