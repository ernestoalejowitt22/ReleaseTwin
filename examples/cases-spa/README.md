<!--
SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
SPDX-License-Identifier: Apache-2.0
-->

# SPA UI-journey cases

Three cases that drive a React or Angular single-page app through a real browser
and bridge a UI-observed value into an API leg:

| case | shows |
|---|---|
| `react-journey.yaml` | route change → `ui.assertText` → capture → API leg, against a React app |
| `angular-journey.yaml` | the same pipeline against an Angular app |
| `admin-cookie.yaml` | `ui.setCookie` to reach a gated view before the first navigation |

These are the **copy-paste reference**. The runnable demo apps
(`apps/react-demo/`, `apps/angular-demo/`) and the pipelines that run these
cases on Bitbucket, Azure Pipelines, and GitHub Actions live in the
[`releasetwin-ci-examples`](https://github.com/ernestoalejowitt22/releasetwin-ci-examples)
repo.

## Running them yourself

The cases resolve two environment variables at load time:

- `SPA_BASE_URL` — the running SPA (e.g. `http://localhost:4173`)
- `API_BASE_URL` — a backend for the API leg (e.g. `https://postman-echo.com`)

The UI adapter is opt-in (it launches a real browser):

```bash
RELEASETWIN_UI_ENABLED=1 SPA_BASE_URL=… API_BASE_URL=… \
  dotnet run --project src/ReleaseTwin.Cli -- run examples/cases-spa
```

Point the cases at your own app by changing `SPA_BASE_URL` and the selectors —
the `[data-testid="…"]` hooks are what to swap. See
[docs/spa-testing.md](../../docs/spa-testing.md).
