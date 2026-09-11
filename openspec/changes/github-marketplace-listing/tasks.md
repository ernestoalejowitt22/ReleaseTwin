## 1. User setup (blocking — do before task 2 can run for real)

- [x] 1.1 **Done.** `releasetwin/releasetwin-action` exists, is public, and carries
      tags `0.3.0` / `v0.3` / `v0`. Originally created under the personal account;
      moved to the `releasetwin` org (commit `23fec43`, PR #141), which is why this
      task's original text named the old path. Standing constraint: the mirror's
      `main` must never get branch-protection rules that block force-pushes or
      require PRs — the mirroring step force-pushes directly (see design.md -
      Decisions).
- [x] 1.2 **Done.** Repo secret `RELEASETWIN_ACTION_MIRROR_TOKEN` is present on
      `ReleaseTwin` (added 2026-09-11). Original instructions kept for the record: generate a fine-grained personal access
      token (GitHub Settings → Developer settings → Fine-grained tokens),
      repository access limited to `releasetwin-action` only, permission
      Contents: Read and write, no expiry or a long one. Add it as a repo secret
      on `ReleaseTwin` named `RELEASETWIN_ACTION_MIRROR_TOKEN`.

## 2. Release workflow: mirror to the dedicated repository

- [x] 2.1 In `.github/workflows/release.yml`, after the existing floating-tag step,
      add a step that runs `git subtree split --prefix=integrations/github-action -b action-mirror`
      against the current checkout. Also switched the job's initial checkout to
      `fetch-depth: 0` — `subtree split` needs full history, and the default
      shallow clone would silently produce an incomplete split. Verified locally:
      `git subtree split --prefix=integrations/github-action` produces a branch
      whose tree is exactly `LICENSE`, `README.md`, `action.yml`, `render.mjs`,
      `render.test.mjs` at root — nothing else.
- [x] 2.2 Push that branch to `releasetwin-action`'s `main`
      (`git push --force https://x-access-token:${RELEASETWIN_ACTION_MIRROR_TOKEN}@github.com/releasetwin/releasetwin-action.git action-mirror:main`),
      then tag the pushed commit with the release version and force-push that tag.
      The step is gated on `secrets.RELEASETWIN_ACTION_MIRROR_TOKEN != ''` so it's
      skipped (not failed) until task 1.2 is done, rather than breaking every
      release in the meantime.
- [x] 2.3 Move `releasetwin-action`'s floating `v<major>` / `v<major>.<minor>` tags
      to the same commit, mirroring the existing "Update floating version tags"
      step's logic but targeting the second remote.
- [x] 2.4 Gate all of 2.1-2.3 on the same success conditions as the rest of the
      job — the step's own failure fails the job once the secret is present;
      no special-casing needed given the job already runs sequentially.

## 3. Documentation

- [x] 3.1 Update `docs/ci.md`'s GitHub Action snippet to lead with
      `uses: releasetwin/releasetwin-action@v0.2.0`, keeping the
      subdirectory form documented as an alternative.
- [x] 3.2 Update `docs/install.md` the same way.
- [x] 3.3 Update `integrations/github-action/README.md`'s usage snippet the same
      way, and add a short note explaining the two published forms are the same
      code (one is the Marketplace-eligible mirror).

## 4. One-time Marketplace listing (after the first successful mirror)

- [ ] 4.1 **Needs the user to run this** — on `releasetwin/releasetwin-action`, "Draft a new
      release" in GitHub's UI (the tag the release job already pushed is
      available to pick), check "Publish this Action to the GitHub Marketplace,"
      choose a primary category, confirm the `branding:` icon/color from
      `action.yml` renders correctly in the preview, and publish. One-time only —
      later releases update automatically via the mirroring step.

## 5. Verification

- [ ] 5.1 Still open — needs the *next* tagged release to verify. Status as of
      2026-09-11: the v0.3.0 release run (34180004553) failed at this step with
      `Permission to ernestoalejowitt22/releasetwin-action.git denied to
      github-actions[bot]`, so the mirror was seeded by hand instead. `release.yml`
      has since been re-pointed at the org path and given the PAT plus the
      `http.https://github.com/.extraheader=` workaround, so the cause is fixed but
      unproven. A `diff -r` on 2026-09-11 shows the mirror lagging `main` by the
      `local-evidence-viewer` changes (the `evidence` input, the export and upload
      steps) — expected, since the mirror only advances on a tagged release. Re-run
      that diff right after the next release; it should come back empty.
- [x] 5.2 Confirmed — `gh api repos/releasetwin/releasetwin-action --jq '.license.spdx_id'`
      returns `Apache-2.0`.
- [x] 5.3 Run `openspec validate github-marketplace-listing --strict` and confirm
      it passes.
