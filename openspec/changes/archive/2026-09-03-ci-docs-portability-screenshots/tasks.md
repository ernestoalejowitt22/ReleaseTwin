## 1. Capture

- [x] 1.1 Screenshot the NuGet package page (`https://www.nuget.org/packages/releasetwin`)
      — the package header + version + `dotnet tool install` snippet area. Save as
      `docs/assets/ci/nuget-package.png`.
- [x] 1.2 Screenshot the GHCR package page
      (`https://github.com/ernestoalejowitt22/releasetwin/pkgs/container/releasetwin%2Fcli`)
      — the package header + pull command. Save as `docs/assets/ci/ghcr-package.png`.
- [x] 1.3 Screenshot a real PR-annotation comment from this repo's own dogfooded Action run
      (e.g. PR #124's `releasetwin-summary` comment) — the comment body only. Save as
      `docs/assets/ci/pr-annotation-comment.png`.
- [x] 1.4 Screenshot the Bitbucket Pipelines
      [build #1](https://bitbucket.org/releasetwin/releasetwin-ci-example-projects/pipelines/results/1)
      results page — the run status + job list. Save as
      `docs/assets/ci/bitbucket-build-1.png`.
- [x] 1.5 Screenshot the Azure Pipelines
      [build #239](https://ernestotesting.visualstudio.com/My%20First%20Project/_build/results?buildId=239)
      results page. The login-walled summary page could not be reached from this session
      (see design.md decision below); the user signed in and captured the Express demo
      job's "Run release-proof cases" step log instead — job list sidebar + real CLI
      output (`dotnet run --project src/ReleaseTwin.Cli`, `PASS EXPRESS-CONTRACT-1`,
      `FLAGPROOF EXPRESS-FLAGPROOF-1 (Passed)`, `2 passed, 0 failed`). Saved as
      `docs/assets/ci/azure-build-239.png`.
- [x] 1.6 Open each of the 5 PNGs and confirm it shows real, readable content (not a
      blank/loading frame, not a cookie-banner overlay, not a login wall) — same bar as
      this project's evidence-quality convention. All 5 verified.

## 2. `docs/ci.md` content

- [x] 2.1 Add the NuGet screenshot next to the existing `dotnet tool install -g releasetwin`
      reference (the "PR annotations" section's commented-out option B, and/or
      `docs/installation-model.md`'s CLI list — confirm the right spot while editing).
- [x] 2.2 Add the GHCR screenshot next to the existing `docker run` / `image:` snippets.
- [x] 2.3 Add the PR-annotation screenshot to the "PR annotations" section, next to the
      existing bullet description of what the comment contains.
- [x] 2.4 Add the Bitbucket and Azure screenshots to the "These aren't just typed —
      Bitbucket is proven live." paragraph, alongside the existing build #1 / #239 links.
- [x] 2.5 Add one short note (not per-image) that these are point-in-time captures — the
      linked pages, not the screenshots, are the source of truth if they ever disagree.

## 3. Verify

- [x] 3.1 Render `docs/ci.md` (or the repo's doc-preview path, if any) and confirm every
      image renders with no broken link. All 5 image paths in docs/ci.md resolve to files
      present in docs/assets/ci/.
- [x] 3.2 `openspec validate ci-docs-portability-screenshots --strict` passes.
- [x] 3.3 Confirm with the user before archiving.
