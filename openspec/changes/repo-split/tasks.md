Ordering matters — the private repo must be populated and deploying before the
public repo is trimmed. Tasks marked **"Needs the user to run this"** touch
GitHub repo creation, AWS OIDC, repo secrets, or Vercel and cannot be done from
here.

## 1. Pre-flight

- [ ] 1.1 Merge or close open PRs so no branch is orphaned by the rewrite —
      currently #108 (`evidence-integrity-and-audit-log` draft) and #50
      (`landing-demo-why-and-portability`). Record the decision for each.
- [ ] 1.2 Finalise the path partition (proposal.md table) — one reviewed list of
      every top-level path and where it goes; resolve the design Open Questions
      (`.claude`/`CLAUDE.md`, `data-export`, `demo/record.sh`).
- [ ] 1.3 Snapshot: `git bundle create ../releasetwin-pre-split.bundle --all` +
      note the current `main` SHA, as a rollback anchor.

## 2. Build the private repo (`releasetwin-platform`)

- [ ] 2.1 **Needs the user to run this** — create the private repo
      `github.com/ernestoalejowitt22/releasetwin-platform` (empty, no README).
- [ ] 2.2 Mirror-clone `ReleaseTwin`; `git filter-repo --path …` with the
      private keep-list (hosted/, web/, hosted/terraform*, private docs/,
      private openspec/specs/, openspec/changes/archive/**, go-public-sequence,
      hosted .github/workflows/). Verify history is intact for kept paths.
- [ ] 2.3 Reconcile the private repo's root: keep `hosted/ReleaseTwin.Hosted.slnx`
      + `docker-compose.yml`; add a private `README.md` + full `CLAUDE.md`;
      `REUSE.toml` scoped to the BUSL + hosted paths.
- [ ] 2.4 Rewrite `go-public-sequence` for the split — 2.4 targets the
      engine-only public `ReleaseTwin`; repo-visibility section re-scoped.
- [ ] 2.5 **Needs the user to run this** — push to `releasetwin-platform`
      (`git push --mirror` to the empty repo, or per-branch).
- [ ] 2.6 Verify in the private repo: `dotnet build hosted/ReleaseTwin.Hosted.slnx`
      + `dotnet test` green; `cd web && npm ci && npm run build` green;
      `openspec validate --all --strict` (hosted specs + go-public-sequence).

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

- [ ] 5.1 Mirror-clone `ReleaseTwin`; `git filter-repo --invert-paths --path …`
      with the private set (same list as 2.2). Verify engine history intact.
- [ ] 5.2 Reconcile root: `ReleaseTwin.sln` → engine projects only;
      `REUSE.toml` → drop `hosted/**`,`web/**` (BUSL) + hosted-path annotations;
      `README.md` rewritten (engine-first, Adapter Linking Exception up front,
      point hosted/pricing at releasetwin.com not a repo dir); `CLAUDE.md` →
      engine-dev version; drop `docker-compose.yml`, hosted `.github/workflows/`.
- [ ] 5.3 `.github/workflows/` audit — `ci.yml` builds only `ReleaseTwin.sln`;
      remove `hosted-ci`, `web-ci`, `deploy-hosted`, `bootstrap`,
      `ld-http-flag-control-e2e`, `releasetwin-demo`; `codeql`/`dependency-scan`
      scoped to what remains.
- [ ] 5.4 Cross-reference audit: `git grep -nE 'ReleaseTwin/(hosted|web)|hosted/terraform|openspec/changes/archive'`
      across the trimmed tree → every hit is stale and fixed or removed.
- [ ] 5.5 **Needs the user to run this** — `git push --force` the trimmed history
      to `ReleaseTwin` (all branches + tags).

## 6. Verify the public side

- [ ] 6.1 Fresh clone of `ReleaseTwin`: `dotnet build ReleaseTwin.sln` +
      `dotnet test ReleaseTwin.sln` green — report counts.
- [ ] 6.2 `openspec validate --all --strict` green (engine specs only).
- [ ] 6.3 `reuse lint` green with the trimmed `REUSE.toml`.
- [ ] 6.4 `npx eslint` / the Action's `node --test` still pass where applicable.
- [ ] 6.5 No `hosted/` / `web/` / `terraform` / archived-change paths anywhere in
      `git rev-list --all` of the trimmed repo.
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
