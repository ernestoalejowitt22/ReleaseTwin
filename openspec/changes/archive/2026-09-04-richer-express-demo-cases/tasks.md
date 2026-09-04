## 1. Confirm CI configs need no changes

- [x] 1.1 Re-read `.github/workflows/express.yml`, `bitbucket-pipelines.yml`,
      and `azure-pipelines.yml` in the sibling `releasetwin-ci-examples` repo
      and confirm each invokes the CLI against the whole
      `examples/cases-express` directory (not a fixed file list). Confirmed
      all three: `dotnet run --project src/ReleaseTwin.Cli -- run
      examples/cases-express` (Bitbucket/Azure) and `run
      /examples/cases-express` (GitHub Actions, containerized, mounts the
      whole `examples/` dir). No fixed file lists anywhere — no workflow
      edits needed.
- [x] 1.2 Confirm this repo's own `.github/workflows/pr-annotations.yml`
      still only scans `examples/cases-http-only` (unaffected either way).
      Confirmed (checked in the prior demo-case-with-attached-evidence
      change too).

## 2. Extend the demo app (releasetwin-ci-examples, separate repo)

- [x] 2.1 In `apps/express-demo/server.js`: add `"currency-normalization":
      "disabled"` to the `flags` object and a `nextOrderId` counter.
- [x] 2.2 Add `POST /orders`: reads `currency`/`subtotal` from the body,
      upper-cases `currency` only when `currency-normalization` is enabled,
      stores and returns the created order as `201`.
- [x] 2.3 Update `apps/express-demo/README.md`: "one real behaviour bug" →
      "two real behaviour bugs, each behind its own flag", with a short
      `curl` example for the new endpoint mirroring the existing one.
- [x] 2.4 Boot the app locally and manually verify with `curl`: confirmed —
      `POST /orders {"currency":"usd","subtotal":100}` → `currency:"usd"`
      while disabled; after `PUT /admin/flags/currency-normalization
      {"state":"enabled"}` → `currency:"USD"`. Also confirmed
      `GET /admin/flags/orders-v2` → `{"key":"orders-v2","state":"disabled"}`
      (task 3.1's target) and the existing `GET /orders/42` unaffected.

## 3. New cases (this repo)

- [x] 3.1 Add `examples/cases-express/flag-state.yaml`
      (`EXPRESS-CONTRACT-FLAGSTATE-1`): `GET /admin/flags/orders-v2`,
      asserting `$.key == "orders-v2"` and `$.state == "disabled"`. Reuse
      `express-orders.json`'s existing recorded `sha256`.
- [x] 3.2 Add `examples/cases-express/currency-flag-proof.yaml`
      (`EXPRESS-FLAGPROOF-CURRENCY-1`): `POST /orders` with
      `{"currency":"usd","subtotal":100}`, asserting `$.currency == "USD"`,
      `flag_proof.feature_key: currency-normalization`. Reuse the same
      fixture.
- [x] 3.3 Run all 4 cases together against the locally booted app and
      confirm `4 passed, 0 failed`. First run caught a real bug: task
      3.1/3.2's original design asserted `orders-v2`'s state directly, but
      that flag is mutated by the sibling flag-proof case in the same
      shared app process, and case files load alphabetically — by the time
      `flag-state.yaml` ran, `flag-proof.yaml` had already toggled it to
      `enabled`, so the assertion failed non-deterministically on run order
      (`3 passed, 1 failed`). Fixed by adding a third flag,
      `maintenance-mode`, that no flag-proof case ever touches, and
      repointing `flag-state.yaml` at it — re-run confirmed `4 passed, 0
      failed`, with both original cases (`EXPRESS-CONTRACT-1`,
      `EXPRESS-FLAGPROOF-1`) unaffected.

## 4. Docs (this repo)

- [x] 4.1 Update `docs/express.md` section "2" (or add a short new section)
      introducing `GET /admin/flags/:key` and the flag-state case.
- [x] 4.2 Update section "3. The flag-proof case" with a paragraph
      introducing `currency-normalization` alongside the existing
      `orders-v2` walkthrough (the tax example stays primary).
- [x] 4.3 Update section "4. Run it" with the real 4-case output from task
      3.3, replacing the stale `2 passed, 0 failed` line.
- [x] 4.4 Update the "1. The app under test" section's description of the
      app to mention it now has two flagged behaviors, matching the sibling
      repo's updated README.

## 5. Verify

- [x] 5.1 `openspec validate richer-express-demo-cases --strict` passes.
- [x] 5.2 Confirm with the user before archiving — and separately confirm
      whether they want the sibling `releasetwin-ci-examples` repo's edits
      committed/pushed (this repo's own edits are covered by the normal
      commit-only-when-asked convention already). User confirmed archiving;
      the sibling-repo commit question is still open (see chat).
