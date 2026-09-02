Ordering matters — the private repo must be populated and deploying before the
public repo is trimmed. Tasks marked **"Needs the user to run this"** touch
GitHub repo creation, AWS OIDC, repo secrets, or Vercel and cannot be done from
here.

## 1. Pre-flight

- [x] 1.1 #108 merged (draft preserved), #50 merged (completed archived work),
      #118 (this proposal) merged. Zero open PRs at split time.
- [x] 1.2 `partition.md` written + reviewed. Open questions resolved: `.claude`/
      `.cursor`/`.agents` public + `CLAUDE.md` trimmed; `data-export` doc public /
      spec private; `demo/record.sh` public (kept as-is — it references `src/`,
      which is also public).
- [x] 1.3 `releasetwin-pre-split.bundle` created; rollback anchor `b7a15b5`.

## 2. Build the private repo (`releasetwin-platform`)

- [ ] 2.1 **Needs the user to run this** — create the private repo
      `github.com/ernestoalejowitt22/releasetwin-platform` (empty, no README).
- [x] 2.2 `filter-repo --paths-from-file private-paths.txt` (44 paths) → private
      tree = `.github/ docs/ hosted/ openspec/ web/`; history intact.
- [x] 2.3 Private root: README + REUSE.toml (BUSL) + LICENSES/ + CLAUDE.md +
      Directory.Build.props + flags.json + **nuget.config** (found missing — the
      hermetic-restore config; without it `dotnet` hit the prior employer's ETI
      CodeArtifact feed via the machine-level NuGet config) + .gitignore +
      .gitleaks.toml + openspec/config.yaml. web demo-video script repointed off
      `../demo/`.
- [x] 2.4 `go-public-sequence` §2 rewritten in the private tree — 2.4 now flips
      the engine-only public repo; 2.2 flagged to re-run against the trimmed
      history.
- [ ] 2.5 **Needs the user to run this** — push to `releasetwin-platform`
      (`git push --mirror` to the empty repo, or per-branch).
- [x] 2.6 Verified locally (no Actions): `dotnet build hosted/…slnx` 0 errors +
      hermetic; `dotnet test` 382 pass; `web` build OK + eslint clean + vitest 13.

## 3. Infra cutover (all **Needs the user to run this**)

- [ ] 3.1 `hosted/terraform-bootstrap/main.tf`: change the OIDC trust condition
      `repo:ernestoalejowitt22/ReleaseTwin:*` → `…/releasetwin-platform:*`;
      commit in the private repo.
- [ ] 3.2 Run the bootstrap workflow from `releasetwin-platform` (creates/updates
      the OIDC provider + deploy role trust for the new repo).
- [ ] 3.3 Move repo secrets + variables `ReleaseTwin` → `releasetwin-platform`:
      `CLERK_*`, `POLAR_*`, `AWS_DEPLOY_ROLE_ARN`, `WEB_BASE_URL`, `DOMAIN_NAME`,
      `NOTIFICATIONS_FROM_ADDRESS`, `CLERK_DOMAIN`, `ADMIN_OPERATOR_USER_IDS`,
      e2e (`E2E_CLERK_*`, `AWS_E2E_ROLE_ARN`, `E2E_TEST_USER_EMAIL`). Leave
      `NUGET_API_KEY` on `ReleaseTwin`.
- [ ] 3.4 Reconnect the Vercel project to `releasetwin-platform` (root dir
      `web/`); re-add its env; trigger a build.
- [ ] 3.5 Configure the new repo: branch protection, `secret-scan` + `codeql`
      workflows present, dependabot config.

## 4. Verify the private side works

- [ ] 4.1 **Needs the user to run this** — push a trivial commit to
      `releasetwin-platform` `main`; confirm `deploy-hosted` assumes its role and
      applies clean (no drift), and `hosted-ci` + `web-ci` are green.
- [ ] 4.2 Confirm the dev stack still serves (`releasetwin.com` sign-in widget,
      hosted API rejects a bad token with 401) — nothing regressed during the
      cutover.
- [ ] 4.3 Vercel Preview + production build green from the new repo.

## 5. Trim the public repo (`ReleaseTwin`)

- [x] 5.1 `filter-repo --invert-paths --paths-from-file private-paths.txt` →
      public tree engine-only; `main` 179 → 67 commits; no hosted/web blob in
      any reachable history.
- [x] 5.2 `ReleaseTwin.sln` was already engine-only. README rewritten;
      LICENSING.md → 2 licenses; REUSE.toml trimmed; CONTRIBUTING + PR template;
      CLAUDE.md engine version; flags.json → cli flag only; `LICENSES/BUSL-1.1.txt`
      deleted; docs/continuity + installation-model re-pointed.
- [x] 5.3 Public workflows: ci, codeql, dependency-scan, pr-annotations, release,
      secret-scan. codeql dropped the hosted-slnx build; dependency-scan dropped
      the hosted loop + the `web:` audit job; both aligned to dotnet 8.0.x.
- [x] 5.4 Sweep clean outside `openspec/` and `docs/feature-flags.md` (the flag
      seam doc still has web/hosted rows — cosmetic, non-blocking; flagged for a
      follow-up polish).
- [ ] 5.5 **Needs the user to run this** — `git push --force` the trimmed history
      to `ReleaseTwin` (all branches + tags).

## 6. Verify the public side

- [x] 6.1 `dotnet build` 0 errors; `dotnet test` 270 pass (10/29/5/49/12/152/13).
- [x] 6.2 `openspec validate --all --strict` — 15/15.
- [ ] 6.3 `reuse lint` green with the trimmed `REUSE.toml`.
- [x] 6.4 `node --test integrations/github-action/render.test.mjs` — 6/6.
- [x] 6.5 Confirmed — `git log --all --name-only` has no `hosted/`/`web/` path.
- [ ] 6.6 `ci.yml` + `pr-annotations` + `release.yml` (dry) sane on the trimmed
      repo.

## 7. Close-out

- [ ] 7.1 Local clones (`~/Projects/ReleaseTwin`, other machines) re-cloned or
      `reset --hard` to the new histories of both repos.
- [ ] 7.2 `openspec/changes/repo-split` moves to `releasetwin-platform` and is
      archived there.
- [ ] 7.3 Update memory: two-repo topology, what lives where, infra wiring.
- [ ] 7.4 `go-public-sequence` in the private repo is now unblocked for 2.4
      (flip the engine-only public repo).
- [ ] 7.5 Confirm with the user before archiving.
