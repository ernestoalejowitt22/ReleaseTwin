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

## 3. Look at the evidence

A pass/fail line is the verdict; the evidence is the product. Set two environment variables
and the CLI writes a redacted record of every case to disk — then `view` serves it as a
browsable report. No account, no sign-up, and no network call of any kind.

```bash
docker run --rm -v "$PWD:/workspace" -w /workspace \
  -e RELEASETWIN_EVIDENCE=on \
  -e RELEASETWIN_EVIDENCE_DIR=/workspace/evidence \
  ghcr.io/ernestoalejowitt22/releasetwin/cli:latest run

docker run --rm -p 8080:8080 -v "$PWD:/workspace" \
  ghcr.io/ernestoalejowitt22/releasetwin/cli:latest view /workspace/evidence
```

`view` always prints the URL to open. Publishing the port with `-p` is what makes the served
report reachable from your host, and the image has no browser of its own to launch — so open
the printed URL yourself. Installed as a `dotnet tool` instead? `releasetwin view ./evidence`
opens your browser for you.

What you get on disk, one directory per case id:

```
evidence/
  CASE-1/
    evidence.json        # the redacted step-by-step record
    <screenshot-id>.png  # one per captured screenshot, best-effort redacted
```

- Redaction runs in your CLI as the evidence is produced — auth headers, credential-shaped
  fields, and resolved `${ENV_VAR}` values are stripped before anything is written.
- **No port to publish?** `view <dir> --export evidence.html` writes one self-contained file
  instead of serving — screenshots inlined, no server, no network — which is what to attach
  to a pull request or a ticket. It is also the form that needs nothing published out of a
  container. The GitHub Action can upload it for you: see [`docs/ci.md`](ci.md).
- Recording UI cases with `RELEASETWIN_UI_VIDEO_DIR`? Point `view` at that directory too
  (`--video-dir`, or the same environment variable) and each case's session plays alongside
  its evidence. Recordings live outside the evidence directory, so the viewer has to be told
  where they are.
- Cases with a failed step are listed first, so the reason a run failed is the first thing
  you see.

## 4. Point it at your own API

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
- `releasetwin view [dir] [--export <file.html>] [--video-dir <dir>]` — render a local
  evidence directory (default: `$RELEASETWIN_EVIDENCE_DIR`, else `./evidence`).
- `releasetwin --help` — all commands.
- The bundled example cases and the [README](../README.md) cover flag proof (the paired
  known-bad / known-good run that tells a broken build from a fixed one) and the hosted
  dashboard.
- [Release-proof your Express API](express.md) — a runnable Node/Express demo with a bug
  behind a feature flag, and the flag proof that catches it. The same `http` case works
  unchanged for Fastify, Nest, and Next.js route handlers.
- [Testing a React or Angular app](spa-testing.md) — drive a SPA through a real browser as
  one leg of a journey: wait on a client-side route, assert rendered text, bridge a
  UI-observed value into an API leg.
- [Enterprise access](enterprise-access.md) — running against a VPN-isolated target and
  authenticating to an API or flag system gated by Microsoft Entra ID / organization OAuth.
