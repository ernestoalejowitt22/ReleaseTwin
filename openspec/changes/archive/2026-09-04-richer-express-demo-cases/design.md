## Context

See proposal.md for motivation. Relevant existing state:

- `releasetwin-ci-examples/apps/express-demo/server.js` (sibling repo,
  `/Users/ernestoalejo/Projects/releasetwin-ci-examples`): one route
  (`GET /orders/:id`), one flag (`orders-v2`), plus the generic
  `GET`/`PUT /admin/flags/:key` pair every flag already shares.
- `examples/cases-express/releasetwin.yml` (this repo): the shared
  `flag_proof.control` template is already parametrized by `{{featureKey}}`
  and `{{state}}` — a new flag needs zero changes here, only a new case
  declaring a different `feature_key`.
- `examples/fixtures/express-orders.json`: unread by any operation (a plain
  `{"note": ..., "order_id": 42}` placeholder) — every case still needs a
  verified fixture per `case-loading`'s requirements, but content is
  irrelevant here.
- `docs/express.md`: a full prose walkthrough that names the exact 2 case
  ids and shows literal CLI output ending `2 passed, 0 failed`.
- `releasetwin-ci-examples/apps/express-demo/README.md`: also names the
  app as "one real behaviour bug" (singular) — becomes stale once a second
  flagged behavior exists.
- All three CI configs already point at the whole `examples/cases-express`
  directory: `.github/workflows/express.yml`,
  `bitbucket-pipelines.yml:26`, `azure-pipelines.yml:34` (all in
  `releasetwin-ci-examples`) — confirmed by direct read during proposal
  research, re-confirmed as task 4.x below.

## Goals / Non-Goals

**Goals:**
- Two more real, passing cases in `examples/cases-express`, one requiring no
  app change (a flag-state contract case) and one requiring a small,
  realistic app extension (a second flag-proof case).
- `docs/express.md` and the sibling repo's `express-demo/README.md` describe
  the app and cases as they now actually are.

**Non-Goals:**
- No changes to `react-demo`/`angular-demo` or their cases — out of scope,
  a separate future change if wanted.
- No new CI workflow files in either repo — both existing case counts should
  just increase automatically once the new files exist, since nothing scans
  a fixed file list.
- No change to the shared `flag_proof.control` template — it's already
  generic.
- Not committing or pushing anything in either repo as part of this
  change's own execution — per this repo's git conventions, that's a
  separate, explicit ask.

## Decisions

**The HTTP adapter's non-2xx-always-fails behavior rules out an
expected-error-status case.** `HttpRequestOperation.cs:122` fails the
`http.request` step itself on any non-2xx response, before any
`http.assertJsonPath` step runs — there is no parameter to say "a 404 (or
400) here is the correct, passing outcome." Both new cases below are
designed to stay entirely within 2xx responses as a result — see proposal.md
for the fuller explanation and why fixing the adapter itself is out of
scope here.

**The second flagged behavior: `POST /orders`, gated by
`currency-normalization`.** Mirrors `orders-v2`'s shape exactly — one flag,
one behavior difference in the response *body* (never the status code), one
assertion that discriminates the two legs — consistent with both the
existing example and the 2xx-only constraint above. Concretely, in
`server.js`:

```js
const flags = { "orders-v2": "disabled", "currency-normalization": "disabled" };
let nextOrderId = 100;

app.post("/orders", (req, res) => {
  const { currency, subtotal } = req.body;
  const normalize = flags["currency-normalization"] === "enabled";
  const resolvedCurrency = normalize && typeof currency === "string" ? currency.toUpperCase() : currency;
  const order = { id: nextOrderId++, currency: resolvedCurrency, subtotal: subtotal ?? 0 };
  orders[order.id] = order;
  res.status(201).json(order);
});
```

Known-bad leg (`currency-normalization` disabled): `POST /orders` with
`{"currency": "usd", "subtotal": 100}` returns `201` with
`currency: "usd"` unchanged — an assertion expecting `"USD"` fails. Known-good
leg (enabled): the same request returns `currency: "USD"` — the assertion
passes. Same fail-then-pass discriminating shape as `EXPRESS-FLAGPROOF-1`,
entirely via the response body, no status-code involvement anywhere.

**The free contract case: `GET /admin/flags/maintenance-mode`.** Rather
than the inexpressible 404 case, this hits a genuinely different part of
the app's existing surface — the flag-introspection endpoint every flag
already shares (`server.js`'s `GET /admin/flags/:key`) — with two plain
JSONPath assertions against its 2xx response: `$.key` and `$.state`.
**Deviation found during implementation**: the plan originally targeted
`orders-v2`, but both `orders-v2` and `currency-normalization` are mutated
by their own flag-proof cases within the same shared, long-lived app
process one CI job boots — case files load alphabetically, so
`flag-proof.yaml` runs (and leaves the flag `enabled`) before
`flag-state.yaml` would check it, making an exact-state assertion on either
flag order-dependent, not a stable "boot-time default". Fixed by adding a
third flag, `maintenance-mode`, that no flag-proof case ever touches —
`server.js` gets one more entry in `flags`, and the case targets that
instead. Still genuinely free (no case behavior change, one extra map
entry in the app), still a different endpoint, now actually stable.

**Fixture: reuse `express-orders.json` for both new cases**, exactly like
the existing two — its content is genuinely unused (confirmed by its own
`"note"` field), so a third/fourth case reusing it with the same recorded
`sha256` is consistent with the existing convention, not a shortcut. No new
fixture file.

**Case ids and file names**: `EXPRESS-CONTRACT-FLAGSTATE-1` in
`examples/cases-express/flag-state.yaml`; `EXPRESS-FLAGPROOF-CURRENCY-1` in
`examples/cases-express/currency-flag-proof.yaml` — following the existing
`contract.yaml` / `flag-proof.yaml` naming (one file per case, filename
describing its shape, not just its id).

**`flag-state.yaml` pipeline** (no flag toggle, no app change):
```yaml
pipeline:
  - operation: http.request
    with:
      method: GET
      url: ${API_BASE_URL}/admin/flags/orders-v2
  - operation: http.assertJsonPath
    with:
      path: $.key
      expected: orders-v2
  - operation: http.assertJsonPath
    with:
      path: $.state
      expected: disabled
```

**`currency-flag-proof.yaml` pipeline**: same shape as `flag-proof.yaml`,
`POST` instead of `GET`, body `{"currency": "usd", "subtotal": 100}`,
asserting `$.currency` equals `USD`, `flag_proof.feature_key:
currency-normalization`.

**Docs**: `docs/express.md` section "3. The flag-proof case" gets a new
paragraph introducing the second flag alongside the tax example (not a full
rewrite — the tax example stays as the primary walkthrough, since it's
simpler); section "4. Run it" gets the updated 4-case output; a short new
mention of the flag-state contract case rounds out section "2" or a new
short section. The sibling repo's `express-demo/README.md` line "one real
behaviour bug" becomes "two real behaviour bugs, each behind its own flag."

## Risks / Trade-offs

- **Cross-repo edit risk**: a mistake in `server.js` could break the
  existing passing cases too — mitigated by running the full case set
  (all 4) against a locally booted `express-demo` before considering this
  done, not just the 2 new cases in isolation.
- **In-memory `orders` state accumulates across `POST` calls within a
  single app process** (`nextOrderId++`) — acceptable, matches the
  existing app's already-in-memory, no-persistence, single-process design;
  each CI run boots a fresh process.
