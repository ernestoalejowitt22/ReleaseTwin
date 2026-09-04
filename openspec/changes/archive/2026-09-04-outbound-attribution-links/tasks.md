## 1. Action: render the attribution footer

- [x] 1.1 In `integrations/github-action/render.mjs`, add an `attribution` option to
      `renderBody` (default `true`) that appends a footer line linking to the
      ReleaseTwin product site after the existing totals/verdict/table content.
- [x] 1.2 Wire a new `RELEASETWIN_ATTRIBUTION` env var into `main()`, parsed the same way
      `RELEASETWIN_COMMENT` / `RELEASETWIN_CHECK` already are, and pass it through to
      `renderBody`.
- [x] 1.3 Confirm `checkPayload`/the check-run body is untouched by the new option (no
      change needed if `renderBody`'s output is only threaded into the comment path,
      but verify the check-run summary doesn't pick up the footer line too).

## 2. Action: expose the opt-out input

- [x] 2.1 Add an `attribution` input to `integrations/github-action/action.yml`
      (boolean-shaped, `required: false`, `default: "true"`), following the existing
      `comment`/`check` input style.
- [x] 2.2 Pass `inputs.attribution` as `RELEASETWIN_ATTRIBUTION` in the render step's
      `env:` block alongside `RELEASETWIN_COMMENT`/`RELEASETWIN_CHECK`.

## 3. Tests

- [x] 3.1 Add a `render.test.mjs` case: `renderBody` with attribution at its default
      includes a link to the product site in the comment body.
- [x] 3.2 Add a case: `renderBody({ attribution: false })` produces a body with no
      product-site link, otherwise identical to the attribution-on render for the same
      summary.
- [x] 3.3 Add a case confirming the check-run payload (`checkPayload`) carries no
      attribution content regardless of the option.
- [x] 3.4 Run `node --test integrations/github-action/` and confirm all cases pass,
      including the pre-existing "renders exactly as before" cases (still true for the
      byte-for-byte historical comparison they cover).

## 4. Documentation links

- [x] 4.1 Add a link to the ReleaseTwin product site near the top of
      `integrations/github-action/README.md`, and document the new `attribution` input
      alongside the other inputs.
- [x] 4.2 Add a link to the ReleaseTwin product site near the top of
      `integrations/gitlab-component/README.md`.
- [x] 4.3 Add a link to the ReleaseTwin product site near the top of
      `src/ReleaseTwin.Cli/README.md`.

## 5. Verification

- [x] 5.1 Run `node --test integrations/github-action/` (per CLAUDE.md verification) and
      report the test count.
- [x] 5.2 Manually inspect a rendered comment body (from a test fixture summary) to
      confirm the footer line reads as attribution, not as part of the pass/fail verdict.
