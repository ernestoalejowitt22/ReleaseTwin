> **Scope reduced 2026-09-03 (after initial apply).** The `examples/express-demo/`
> app and the `express-example` GitHub Actions job were **removed from this
> change** and moved to `ci-portability-live-examples` (a `releasetwin-ci-examples`
> repo running the demo through Bitbucket / Azure / GitHub pipelines). This change
> now delivers `examples/cases-express/` (the reference case YAML) + `docs/express.md`
> + the README pointers. Groups 1 and 3 below were built then reverted; kept for
> history.

## 1. Demo Express app (`examples/express-demo/`) — REVERTED, moved to ci-portability-live-examples

- [x] 1.1 `package.json` — `express` only, `engines.node` >=20, `start` script; Apache-2.0 license field
- [x] 1.2 Commit `package-lock.json` from a clean `npm install` (express 5.2.1, 68 deps)
- [x] 1.3 `server.js` — in-memory flag store, `GET /orders/:id` (total omits tax unless `orders-v2` on), `PUT /admin/flags/:key` + `GET /admin/flags/:key` (no auth), `GET /healthz`, `PORT` env default 4599. (PUT not POST — matches the existing `cases-flag-proof-shared-control` control template.)
- [x] 1.4 SPDX `Apache-2.0` header on `server.js`; `examples/**` already Apache-2.0 in `REUSE.toml` (override precedence) — no edit needed
- [x] 1.5 `examples/express-demo/README.md` — what it is, `npm ci && npm start`, why the Node toolchain is isolated here

## 2. Case files (`examples/cases-express/`)

- [x] 2.1 `releasetwin.yml` — shared `flag_proof.control` template (`PUT` to `${API_BASE_URL}/admin/flags/{{featureKey}}`, body with `{{state}}`) plus a `verify` block (`GET` the same, `json_path: $.state`)
- [x] 2.2 `flag-proof.yaml` — `flag_proof` with `feature_key` + `build_identity` only; oracle + fixture; pipeline asserts `$.taxed == true`; comment explains known-bad/known-good and why `verify` is safe here
- [x] 2.3 `contract.yaml` — plain `http.request` + two `http.assertJsonPath`, no flag
- [x] 2.4 Fixture `examples/fixtures/express-orders.json` (the loader resolves `../fixtures` from the cases dir, same as `cases-flag-proof-shared-control`); sha256 `2e719aca…c9d0` in both case files
- [x] 2.5 Ran locally with `API_BASE_URL=http://localhost:4599`: `PASS EXPRESS-CONTRACT-1` + `FLAGPROOF EXPRESS-FLAGPROOF-1 (Passed)`, exit 0; `--summary-json` shows `flagProof: { proven: 1 }` and the demo flag ends `enabled` (known-good leg last)

## 3. CI

- [x] 3.1 Added an `express-example` job to `.github/workflows/ci.yml`. Deviation from design D3: runs on **every** PR (no `dorny/paths-filter`, no `schedule`) — a parallel ~20s job that breaks any PR breaking the example is strictly stronger coverage than path-filtering, and the repo has no existing nightly infra to hook into. The .NET `build-and-test` job is untouched.
- [x] 3.2 Steps: checkout, setup-dotnet + setup-node@22 (npm cache keyed on the lockfile), `npm ci` in `examples/express-demo/`, build CLI, boot server + poll `/healthz` (30×0.5s), `dotnet run -- run examples/cases-express` with `API_BASE_URL`, always-stop the server
- [x] 3.3 `gh auth` token scopes include `workflow` — confirmed
- [ ] 3.4 Verify the job passes on a branch (green run link in the PR) — needs a push; deferred to when the branch goes up

## 4. Docs & discoverability

- [x] 4.1 `docs/express.md` — run the demo, walk the contract + flag-proof cases, point it at your own service; Fastify/Nest/Next section; explicit "no REST surface still needs a bespoke adapter" caveat
- [x] 4.2 `README.md` — Express example row in the "What's here" table + a paragraph under "Running the example"
- [x] 4.3 `docs/quickstart.md` — cross-link added to the "More" list
- [x] 4.4 Grepped `README.md` / `docs/*.md`: the new page's "any REST API, no adapter; non-REST still needs adapter code" framing matches README §"What's not built yet" and `installation-model.md`

## 5. Verification

- [x] 5.1 `dotnet build` + `dotnet test ReleaseTwin.sln` — 270 passed, 0 failed, 0 skipped (unchanged; no engine edit)
- [x] 5.2 GitHub Action tests — 6/6 pass via `node --test "integrations/github-action/**/*.test.mjs"`. Note: the bare `node --test integrations/github-action/` form in CLAUDE.md fails on Node 22.14 (trailing-slash dir arg is treated as a module path) — pre-existing, unrelated to this change.
- [x] 5.3 `openspec validate express-flag-proof-example --strict` — valid
- [x] 5.4 No evidence folder for this example — the deliverable is the flag-proof verdict. Verified: at rest `GET /orders/42` → `taxed:false`; run output `FLAGPROOF EXPRESS-FLAGPROOF-1 (Passed)` (known-bad leg fails `$.taxed==true`, known-good passes); `--summary-json` → `flagProof.proven: 1`, per-case `flagProof: "Passed"`; demo flag left `enabled` (known-good leg ran last). Matches `docs/express.md` §3–4.
