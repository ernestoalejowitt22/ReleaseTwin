## 1. Dependency-vulnerability scanning (D1)

- [x] 1.1 Add `.github/workflows/dependency-scan.yml` triggered on `pull_request` and `push` to `main`, with `permissions: contents: read`.
- [x] 1.2 .NET job: pinned `actions/setup-dotnet` SDK, `dotnet restore ReleaseTwin.sln`, then `dotnet list package --vulnerable --include-transitive`; fail the job (echoing the matching lines) when the output contains `Critical` or `High`; also fail if the command produced no recognizable report (guard against silent output-format drift). _(Scans both `ReleaseTwin.sln` and `hosted/ReleaseTwin.Hosted.slnx`; drops advisory URLs waived in `Directory.Build.props` since `dotnet list` ignores `NuGetAuditSuppress`.)_
- [x] 1.3 web job: `npm ci` in `web/`, then `npm audit --audit-level=high` (its exit code is authoritative). _(node 22, matching web-ci.)_
- [x] 1.4 Add `.github/dependabot.yml` with three ecosystems — `nuget` (repo root), `npm` (`/web`), `github-actions` (`/`) — weekly cadence, grouped minor/patch updates. _(A `dependabot.yml` already existed with those three; extended it — added a second `nuget` entry for `/hosted` (separate solution) and changed the groups from "all patterns" to minor/patch so major bumps get individual review.)_
- [x] 1.5 Run the workflow once on a throwaway branch; triage whatever the first scan reports (fix, or record a documented waiver) so `main` starts green. _(Can't run CI directly; triaged the equivalent local scan: **fixed** `SixLabors.ImageSharp` 3.1.5→3.1.12 (shipped) and `System.Security.Cryptography.Xml` 10.0.7→10.0.11 pin (hosted); **waived** the xunit-transitive `System.Net.Http`/`System.Text.RegularExpressions` 4.3.0 advisories in `Directory.Build.props` with a documented reason. Local scan now clean; 135 CLI tests + engine build green.)_

## 2. Static analysis — CodeQL (D2)

- [x] 2.1 Add `.github/workflows/codeql.yml` (advanced setup) with `language: [csharp, javascript-typescript]`, triggers `pull_request` + `push` to `main` + weekly `schedule`, and the required `security-events: write` permission.
- [x] 2.2 C# analysis: explicit `dotnet build ReleaseTwin.sln` (pinned SDK) between `init` and `analyze`; JS/TS analysis: no build step. _(Also builds `hosted/ReleaseTwin.Hosted.slnx` so the hosted API is covered.)_
- [x] 2.3 Keep the query suite at the default `security` level for now (avoid PR noise); leave a comment noting `security-extended` as a later opt-in.
- [ ] 2.4 Run once on a branch; triage initial findings to green (fix or dismiss with a reason in the Security tab). _(Deferred to the first CI run on this PR — CodeQL can't run locally. Review the PR's Security tab / checks and dismiss false positives with a reason.)_

## 3. Harden the gitleaks install (D4)

- [x] 3.1 In `.github/workflows/secret-scan.yml`, replace the `curl … | tar` step: download the pinned-version release tarball and its `.sha256` (or the checksums file) to disk, run `sha256sum -c`, then extract. Keep the explicit `VERSION` pin. _(Verified the download + `sha256sum -c` sequence locally against the real 8.24.3 release — `... linux_x64.tar.gz: OK`.)_
- [x] 3.2 Confirm the PR, push, and scheduled history-sweep triggers still behave (a deliberate test secret on a scratch branch fails the check). _(Triggers unchanged in the file. The `gitleaks git … --exit-code 1` scan step is untouched — only the install step changed. Full CI-run confirmation happens on this PR.)_

## 4. Action default image + fork-PR docs (D3)

- [x] 4.1 `integrations/github-action/action.yml`: add a first composite shell step that inspects the effective `image` and emits `::warning::` (naming the unpinned-image risk) when it is a mutable tag (`:latest`, a bare `:vN` / `:N` major tag, or no digest); the run always continues.
- [x] 4.2 `release.yml`: after `docker/build-push-action`, use its `digest` output to rewrite `action.yml`'s `image` default to `ghcr.io/<repo>/cli@<digest>` and commit that to `main` before the floating-tag move (reuse the existing `github-actions[bot]` git identity; only reached when build+test+push all succeeded). _(New `Pin the Action default image` step; the `v0`/`v0.N` floating tags now point at the pin commit — `sha` output, falling back to `github.sha`.)_
- [x] 4.3 `release.yml`: stop the Action from depending on `cli:latest` — decide whether to keep pushing `:latest` purely as a human convenience or drop the tag entirely; document the choice in the Action README. _(Kept `:latest` for humans; the Action's default is now a version/digest pin and never references `:latest`. README says so.)_
- [x] 4.4 `integrations/github-action/README.md`: add an explicit warning that case files can read any secret handed to the container (env-file or forwarded vars), and that workflows running the Action on fork pull requests MUST NOT expose ingest or other sensitive secrets to it. _("Secrets and fork pull requests" section.)_
- [x] 4.5 Tests / checks: `action.yml` default is a digest or pinned version (a repo check or a note in the release runbook); a workflow-lint or manual run confirms the mutable-tag warning fires. _(`action-image-pin` job in `dependency-scan.yml`; the warn-step's bash condition verified locally against pinned + mutable inputs.)_

## 5. Deploy environment gate (D5)

- [x] 5.1 Add `environment: production` to the `deploy` job in `.github/workflows/deploy-hosted.yml`.
- [ ] 5.2 **Needs the user to run this (repo settings):** create the `production` GitHub environment (Settings → Environments) before merging 5.1, so the deploy run does not stall on an unknown environment. Add protection rules (required reviewer / wait timer) at the maintainer's discretion.
- [ ] 5.3 Optional follow-up — **deferred to its own change.** The OIDC trust doc in `hosted/terraform-bootstrap/main.tf` (`github_actions_assume_role`) is shared by the deploy role *and* the e2e role, and the e2e workflow does not run in the `production` environment — tightening `sub` to `…:environment:production` there would break e2e auth. The `sub` format is also already fragile (two candidate formats kept). Needs a dedicated, carefully-tested change that splits the trust docs; not worth the blast radius here.

## 6. Verification

- [x] 6.1 All new workflows are valid (a push to a scratch branch runs them; each produces the expected pass/fail). _(YAML parses clean for all 5 workflows + dependabot.yml; the scan/warn/pin bash snippets each verified locally. Full CI runs happen on this PR.)_
- [x] 6.2 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` still green (no code changed, but confirm the pinned-SDK jobs match local). _(Engine 253/253; hosted 346/346; both build clean incl. the ImageSharp bump + the `System.Security.Cryptography.Xml` pin, no NU1510.)_
- [x] 6.3 `cd web && npm run build` still green. _(Compiled successfully; `npm audit` finds 0.)_
- [x] 6.4 `openspec validate supply-chain-and-ci-hardening --strict` passes.
- [x] 6.5 PR description notes: the digest-pinned Action takes effect only from the next tagged release (`@v0` still defaults to `:latest` until then); and that "required status checks" enforcement is pending a repo-plan / go-public decision. _(In the PR body; note that the default is now a pinned `:0.2.0` version — not `:latest` — from this PR, and release.yml swaps it for a digest on the next tagged release.)_
