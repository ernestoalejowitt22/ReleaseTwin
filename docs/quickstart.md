# Test your first API in 10 minutes

No account, no clone, no .NET SDK — just Docker. (Prefer a `dotnet tool` or the
GitHub Action? See [`docs/install.md`](install.md).)

## 1. Scaffold a project

```bash
mkdir my-release-proof && cd my-release-proof

docker run --rm -v "$PWD:/workspace" -w /workspace \
  ghcr.io/ernestoalejowitt22/releasetwin/cli:latest init
```

You now have:

```
cases/starter.yaml       a commented starter case
fixtures/starter.json     its payload
releasetwin.yaml          project config (optional)
.gitignore
```

## 2. Run it

```bash
docker run --rm -v "$PWD:/workspace" -w /workspace \
  ghcr.io/ernestoalejowitt22/releasetwin/cli:latest run
```

```
PASS starter
1 passed, 0 failed
```

The starter case hits a public test API (`jsonplaceholder.typicode.com`) and asserts on the
JSON — it needs no credentials, so this works on the first try.

## 3. Point it at your own API

Open `cases/starter.yaml` and change the `http.request` URL and the `http.assertJsonPath`
lines. Real URLs and tokens go in as `${ENV_VAR}` — resolved at run time, never committed.

Optionally add a `release:` label near the top of the file — a free-form string (a version, a
sprint, an epic key) that the hosted platform groups cases by into a per-release readiness
rollup. It has no effect on execution:

```yaml
id: starter
release: "4.2"
```

```yaml
pipeline:
  - operation: http.request
    with:
      method: POST
      url: ${API_BASE_URL}/orders
      headers:
        Authorization: Bearer ${API_TOKEN}
      body:
        productId: 123
  - operation: http.assertJsonPath
    with:
      path: $.status
      expected: confirmed
```

Pass the env vars into the container:

```bash
docker run --rm -v "$PWD:/workspace" -w /workspace \
  -e API_BASE_URL -e API_TOKEN \
  ghcr.io/ernestoalejowitt22/releasetwin/cli:latest run
```

A non-zero exit code means a case failed — wire it straight into CI.

## Choosing adapters — `releasetwin.yaml`

By default the CLI loads every adapter whose credentials it finds. To pin the set, list them:

```yaml
# releasetwin.yaml
adapters:
  - http
  - launchdarkly
```

Only listed adapters are considered (`http` is always available). A listed adapter with no
credentials is a **startup error** — you asked for it, so a missing `LAUNCHDARKLY_*` is a
mistake, not a silent skip. Credentials themselves never go in this file.

## More

- `releasetwin new ORDERS-2` — add another case + fixture.
- `releasetwin init --from-examples` — start from the full bundled `examples/` set instead of
  the single starter (Azure DevOps, LaunchDarkly flag-proof, a browser journey).
- `releasetwin --help` — all commands.
- The bundled example cases and the [README](../README.md) cover flag proof (the paired
  known-bad / known-good run that tells a broken build from a fixed one) and the hosted
  dashboard.
- [Enterprise access](enterprise-access.md) — running against a VPN-isolated target and
  authenticating to an API or flag system gated by Microsoft Entra ID / organization OAuth.
