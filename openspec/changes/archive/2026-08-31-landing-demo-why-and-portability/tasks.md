## 1. Shared Bitbucket snippet (D3)

- [x] 1.1 Extract the `bitbucket-pipelines.yml` string from
      `web/src/app/(marketing)/docs/ci/page.tsx` into a shared module
      (`web/src/lib/ci-snippets.ts`) exporting the snippet string and its `label`.
- [x] 1.2 Update `docs/ci/page.tsx` to import and render the snippet from the shared
      module; confirm the rendered output is byte-identical to before.

## 2. Pipeline-log SVG asset (D1, D2)

- [x] 2.1 In `web/scripts/capture-landing-demo.mjs`, add a `pipeline-log.svg` output
      rendered from `web/scripts/demo-summaries/failed.json` — the CLI's per-case lines
      for that run plus a final non-zero-exit / "step failed" line, in a neutral CI-log
      style with no GitHub or Bitbucket chrome.
- [x] 2.2 Regenerate assets and commit `web/public/demo/pipeline-log.svg`.
- [x] 2.3 Review `pipeline-log.svg` against the landing-demo "test data only" requirement
      — no credential-shaped values, no non-test content.

## 3. Non-GitHub CI panel on the landing page (marketing-site + landing-demo specs)

- [x] 3.1 Add a panel entry to the CI-loop section of
      `web/src/app/(marketing)/page.tsx` that renders the shared Bitbucket snippet
      (from task 1.1) above `pipeline-log.svg`.
- [x] 3.2 Give the panel a caption stating the ReleaseTwin verdict is produced the same
      way on any CI from the same `--summary-json` contract, and that this is not a
      packaged Bitbucket integration.
- [x] 3.3 Verify no Bitbucket pull-request screenshot or packaged-integration wording
      appears anywhere on the page.

## 4. Problem / value section (marketing-site spec, D4, D5)

- [x] 4.1 Add a section between the hero and the animated terminal SVG: a short lead-in
      naming the failure mode (contract drift / expired sandbox creds / downstream
      failure under real auth, found post-release) plus a compact "without ReleaseTwin"
      vs "what you gain" two-column list.
- [x] 4.2 Ensure each "what you gain" row restates a claim already made elsewhere on the
      site or in `/docs` (required check; readable PR verdict; linkable redacted
      evidence; execution + data stay in your runner; flag-proof of a real credentialed
      path).
- [x] 4.3 Confirm section order per D5: hero → problem/value → animated SVG → GitHub
      panels → non-GitHub CI panel → dashboard panels → trust → features. The animated
      SVG must not sit above the problem/value section.

## 5. Docs and provenance

- [x] 5.1 Add a row for `pipeline-log.svg` to the assets table in `docs/landing-demo.md`
      (panel, source generator, the `failed.json` it derives from).
- [x] 5.2 Note in `docs/landing-demo.md` that the landing Bitbucket snippet and the
      `/docs/ci` snippet are one shared constant.

## 6. Verification

- [x] 6.1 `npm run build` (next build) in `web/` — passes.
- [x] 6.2 `npx eslint` in `web/` — clean.
- [x] 6.3 Run the web vitest suite and `capture-landing-demo` checks — green; add/adjust
      a test asserting the two Bitbucket snippets are identical if one does not exist.
- [x] 6.4 `openspec validate landing-demo-why-and-portability --strict` — passes.
- [x] 6.5 Visually confirm the rendered landing page: problem/value section reads as the
      setup for the panels; the non-GitHub panel shows config + log and is clearly not a
      PR screenshot.
