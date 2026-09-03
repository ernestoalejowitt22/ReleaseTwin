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

This page walks [`examples/cases-express/`](../examples/cases-express/) — a
contract case and a **flag-proof** case against an Express API — using the
runnable demo app in the
[`releasetwin-ci-examples`](https://github.com/ernestoalejowitt22/releasetwin-ci-examples)
repo (`apps/express-demo/`), which also runs these exact cases on Bitbucket
Pipelines, Azure Pipelines, and GitHub Actions.

## 1. The app under test

`apps/express-demo/server.js` (in `releasetwin-ci-examples`) is ~60 lines.
`GET /orders/:id` returns an order total that **omits tax** unless the
`orders-v2` flag is on:

```bash
git clone https://github.com/ernestoalejowitt22/releasetwin-ci-examples
cd releasetwin-ci-examples/apps/express-demo
npm ci
npm start          # http://localhost:4599

curl localhost:4599/orders/42
# {"id":42,"currency":"USD","subtotal":100,"total":100,"taxed":false}   <- the bug
```

Flag state is in memory and flips over HTTP — `PUT /admin/flags/orders-v2
{"state":"enabled"}` — which is exactly the surface `flag_proof.control` drives
when a flag lives in a system with no dedicated adapter (Unleash, Flagsmith, a
config service, your own endpoint).

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

## 4. Run it

```bash
# with examples/express-demo running and reachable at $API_BASE_URL
API_BASE_URL=http://localhost:4599 \
  dotnet run --project src/ReleaseTwin.Cli -- run examples/cases-express
```

```
PASS EXPRESS-CONTRACT-1
FLAGPROOF EXPRESS-FLAGPROOF-1 (Passed)
2 passed, 0 failed
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
