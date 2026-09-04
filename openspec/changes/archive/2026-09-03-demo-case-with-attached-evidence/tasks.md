## 1. New example case

- [x] 1.1 Add `examples/cases-ui-journey/cases/example-ui-journey-demo-failure.yaml`
      (id `UI-JOURNEY-DEMO-FAILURE-1`): same login flow as the existing passing case
      (`ui.navigate` → `ui.fill` x2 → `ui.click` → `ui.waitFor` → `ui.assertVisible`),
      plus one more `ui.assertText` step on `#flash` asserting `equals: "Welcome back,
      valid user!"` — text the real page never shows. Include an inline comment stating
      this case is an intentional, permanent demo failure, not a bug to fix.
- [x] 1.2 Reuse the existing fixture (`example-ui-journey.json`) rather than adding a
      new one, unless the new case's oracle locator needs its own — confirm while
      writing.
- [x] 1.3 Run the CLI against just this case with `RELEASETWIN_UI_ENABLED=1` and confirm
      it fails with a genuine assertion mismatch (not a crash, not a selector-not-found,
      not a timeout) — capture the exact `FAIL` line's text for use in docs/ci.md later.
      Confirmed:
      `FAIL UI-JOURNEY-DEMO-FAILURE-1 (Product): element '#flash' text was 'You logged
      into a secure area!\n×', expected exactly 'Welcome back, valid user!'` — a real
      assertion mismatch (observed text includes the page's real close-button glyph).

## 2. Capture the evidence

- [x] 2.1 Run the case with `RELEASETWIN_EVIDENCE=on` and `RELEASETWIN_EVIDENCE_DIR`
      pointed at a scratch directory (per design.md's command). Confirmed: writes
      `UI-JOURNEY-DEMO-FAILURE-1/evidence.json` + 7 screenshots (one per step), plus the
      passing sibling case's evidence — both destinations, no token configured.
- [x] 2.2 Open the produced `evidence.json` and screenshot PNG directly and confirm both
      are real. Evidence: step index 6 (`ui.assertText`) is marked `"outcome": "Failed"`
      with its real `Parameters` (`selector: "#flash"`, `equals: "Welcome back, valid
      user!"`) and screenshot id `06e21fe7...`. One nuance found: this step's JSON entry
      has no separate `assertion` (expected/observed) object populated — only the console
      `FAIL` line carries the observed text; `Parameters.equals` carries the expected
      side. Not a local-evidence-artifacts gap (identical for hosted upload — this is how
      `ui.assertText` evidence is shaped generally, out of this change's scope per
      design.md's non-goals) — noted here, not fixed. The screenshot itself
      (`06e21fe7...png`) is real and non-blank: it shows the-internet.herokuapp.com's
      actual "Secure Area" page, green banner "You logged into a secure area!", "Welcome
      to the Secure Area..." text, and a Logout button — the exact real page state the
      failed assertion evaluated against.
- [x] 2.3 Save the verified screenshot as `docs/assets/ci/ui-failure-evidence.png`
      (1280×720).

## 3. Wire into docs/ci.md

- [x] 3.1 Add a new subsection after "PR annotations" ("What a failure looks like")
      pairing the real `FAIL UI-JOURNEY-DEMO-FAILURE-1` line with the screenshot, plus a
      sentence noting it was captured locally via `RELEASETWIN_EVIDENCE_DIR` with no
      hosted account, cross-referencing the Credentials section.
- [x] 3.2 Add a note to `examples/cases-ui-journey/README.md` explaining that
      `example-ui-journey-demo-failure.yaml` is an intentional, permanent demo failure
      excluded from this repo's own `pr-annotations.yml` CI gate on purpose — not
      something to "fix".
- [x] 3.3 Confirm `example-ui-journey-demo-failure.yaml` is NOT picked up by
      `.github/workflows/pr-annotations.yml`. Confirmed: that workflow's `cases-path` is
      `examples/cases-http-only` — `examples/cases-ui-journey/` is never scanned. No
      workflow change needed.

## 4. Verify

- [x] 4.1 Render `docs/ci.md` and confirm the new image renders with no broken link and
      reads coherently next to the existing sections. All 6 `assets/ci/*.png` references
      resolve to real files on disk; new "What a failure looks like" section sits between
      "PR annotations" and "Other CI platforms".
- [x] 4.2 Re-run the full existing CI-relevant case set (`examples/cases-http-only`, the
      only path `pr-annotations.yml` scans) — unchanged: `PASS HTTP-DEMO-1`, `1 passed, 0
      failed`, identical to before this change.
- [x] 4.3 `openspec validate demo-case-with-attached-evidence --strict` passes.
- [x] 4.4 Confirm with the user before archiving.
