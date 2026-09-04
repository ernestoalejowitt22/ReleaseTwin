<!--
SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
SPDX-License-Identifier: AGPL-3.0-only WITH LicenseRef-ReleaseTwin-Adapter-Exception
-->

# Release-proof your Express API

ReleaseTwin tests **any REST API from case-file data alone** — there is no
adapter, plugin, or SDK to install for Express, Fastify, Nest, or a Next.js route
handler. The engine is .NET, but it never touches your code: it makes HTTP calls
and asserts on the responses, so the language your API is written in does not
matter.

This page walks [`examples/cases-express/`](../examples/cases-express/) — two
contract cases and two **flag-proof** cases against an Express API — using the
runnable demo app in the
[`releasetwin-ci-examples`](https://github.com/ernestoalejowitt22/releasetwin-ci-examples)
repo (`apps/express-demo/`), which also runs these exact cases on Bitbucket
Pipelines, Azure Pipelines, and GitHub Actions.

## 1. The app under test

`apps/express-demo/server.js` (in `releasetwin-ci-examples`) is a small
Express app with **two real behaviour bugs, each behind its own flag**:
`GET /orders/:id` returns an order total that **omits tax** unless the
`orders-v2` flag is on, and `POST /orders` doesn't upper-case a lowercase
currency code unless `currency-normalization` is on:

```bash
git clone https://github.com/ernestoalejowitt22/releasetwin-ci-examples
cd releasetwin-ci-examples/apps/express-demo
npm ci
npm start          # http://localhost:4599

curl localhost:4599/orders/42
# {"id":42,"currency":"USD","subtotal":100,"total":100,"taxed":false}   <- bug 1

curl -X POST -H 'Content-Type: application/json' \
  -d '{"currency":"usd","subtotal":100}' localhost:4599/orders
# {"id":100,"currency":"usd","subtotal":100}   <- bug 2
```

Flag state is in memory and flips over HTTP — `PUT /admin/flags/orders-v2
{"state":"enabled"}` (or `.../currency-normalization`) — which is exactly the
surface `flag_proof.control` drives when a flag lives in a system with no
dedicated adapter (Unleash, Flagsmith, a config service, your own endpoint).

## 2. The contract case

[`examples/cases-express/contract.yaml`](../examples/cases-express/contract.yaml)
is the no-flag path — two JSONPath assertions against a live response:

```yaml
pipeline:
  - operation: http.request
    with:
      method: GET
      url: ${API_BASE_URL}/orders/42
  - operation: http.assertJsonPath
    with: { path: $.id, expected: 42 }
  - operation: http.assertJsonPath
    with: { path: $.currency, expected: USD }
```

Real URLs and tokens are always `${ENV_VAR}` — resolved at load time, never
committed.

A second contract case,
[`examples/cases-express/flag-state.yaml`](../examples/cases-express/flag-state.yaml)
(`EXPRESS-CONTRACT-FLAGSTATE-1`), hits a different part of the app's surface:
the flag-introspection endpoint every flag shares
(`GET /admin/flags/:key`), asserting the real boot-time default of a
plain operational flag (`maintenance-mode`) — not `orders-v2` or
`currency-normalization`, since those two are actively toggled by the
flag-proof cases below within the same running app process, so their state at
any point depends on run order.

## 3. The flag-proof case

[`examples/cases-express/flag-proof.yaml`](../examples/cases-express/flag-proof.yaml)
adds a `flag_proof` block. The CLI then runs the pipeline **twice against the
same build** — once with `orders-v2` off, once on:

| leg | flag | `$.taxed` | assertion |
|-----|------|-----------|-----------|
| known-bad  | `disabled` | `false` | **fails** |
| known-good | `enabled`  | `true`  | **passes** |

One leg fails, the other passes → verdict **`Passed`**: the oracle genuinely
discriminates. If both legs passed you would get `WeakOracle`; if the toggle
itself failed, `ControlFailed`. See [flag-proof.md](flag-proof.md) for every
outcome.

The toggle template is declared once in
[`examples/cases-express/releasetwin.yml`](../examples/cases-express/releasetwin.yml),
including a `control.verify` read-back — safe here because the demo's in-memory
flag store is read-your-writes.

A second flag-proof case,
[`examples/cases-express/currency-flag-proof.yaml`](../examples/cases-express/currency-flag-proof.yaml)
(`EXPRESS-FLAGPROOF-CURRENCY-1`), reuses the same template with a different
`feature_key`: `currency-normalization`. It `POST`s `{"currency":"usd",
"subtotal":100}` to `/orders` and asserts `$.currency` equals `"USD"` — the
known-bad leg leaves it lowercase (fails), the known-good leg upper-cases it
(passes). Both legs return `201`; the HTTP adapter fails a request step on any
non-2xx response, so this flag's behavior difference — like `orders-v2`'s —
lives entirely in the response body, never the status code.

## 4. Run it

```bash
# with examples/express-demo running and reachable at $API_BASE_URL
API_BASE_URL=http://localhost:4599 \
  dotnet run --project src/ReleaseTwin.Cli -- run examples/cases-express
```

```
PASS EXPRESS-CONTRACT-1
FLAGPROOF EXPRESS-FLAGPROOF-CURRENCY-1 (Passed)
FLAGPROOF EXPRESS-FLAGPROOF-1 (Passed)
PASS EXPRESS-CONTRACT-FLAGSTATE-1
4 passed, 0 failed
```

A non-zero exit code means a case failed — wire it straight into CI (see
[ci.md](ci.md)). These cases run on every push in `releasetwin-ci-examples`
across Bitbucket Pipelines, Azure Pipelines, and GitHub Actions, with the
JUnit-XML report feeding each platform's native test view.

## 5. Point it at your own service

Copy `examples/cases-express/` next to your project, change the URLs and
assertions, and set the env vars in your pipeline. Nothing about the case is
Express-specific:

- **Fastify / Nest / Hapi** — same `http.request` case, no change.
- **A Next.js / Remix route handler** — it is an HTTP endpoint; same case.
- **GraphQL** — a `POST` with a query body and a JSONPath assertion on
  `$.data.…`.
- **A flag in LaunchDarkly** — drop the `control` block and let the
  [LaunchDarkly adapter](../README.md#flag-proof) drive the toggle instead.

The only thing ReleaseTwin cannot test from case data alone is a service with
**no REST surface** — a gRPC-only service, a queue consumer, a vendor SDK call.
Those still need bespoke adapter code.
