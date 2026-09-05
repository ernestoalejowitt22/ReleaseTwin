<!--
SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
SPDX-License-Identifier: AGPL-3.0-only WITH LicenseRef-ReleaseTwin-Adapter-Exception
-->

# Installing the ReleaseTwin CLI

Three ways to get the CLI. They run the same binary — same case/fixture layout,
same `${ENV_VAR}` interpolation, same non-zero exit on any case failure.

## Container image — no .NET needed

The zero-dependency path. Anything with Docker.

```bash
docker run --rm -v "$PWD:/workspace:ro" \
  ghcr.io/ernestoalejowitt22/releasetwin/cli:0.2.0 /workspace/cases
```

- Pin a released version (`:0.2.0`), not `:latest`, in CI.
- Mount the directory that holds `cases/` and its sibling `fixtures/` at `/workspace`.
- The image bundles `examples/` at `/opt/releasetwin/examples` for offline `init`.

## .NET global tool — you already have .NET

```bash
dotnet tool install --global releasetwin --version 0.2.0
releasetwin ./cases
```

- Needs the .NET runtime (whatever `dotnet --version` reports must cover the
  CLI's target framework).
- UI-journey cases drive a real browser — run `playwright install` once. HTTP
  and flag-proof cases need nothing extra.
- Update with `dotnet tool update --global releasetwin`.

## GitHub Action — in CI, with PR feedback

Runs the CLI and renders the result as a PR comment + a `ReleaseTwin` check run.
Uses only the workflow's `GITHUB_TOKEN` — no ReleaseTwin account.

```yaml
permissions:
  contents: read
  pull-requests: write
  checks: write
jobs:
  release-proof:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: ernestoalejowitt22/releasetwin-action@v0.2.0
        with:
          cases-path: cases
```

See [`docs/ci.md`](ci.md) and
[`integrations/github-action/README.md`](../integrations/github-action/README.md).

## Which one

| You want… | Use |
|---|---|
| To try it with no setup | Container image |
| The CLI on your laptop / a non-Docker runner | `dotnet tool` |
| A merge gate with PR comments | GitHub Action |
| To run entirely offline after one pull | Container image (bundled examples) |

## From source

`git clone` + `dotnet run --project src/ReleaseTwin.Cli -- ./cases` — for
contributing to the engine itself. Not needed to use it.

## Not yet packaged

Homebrew tap and self-contained per-RID single-file binaries (no runtime at
all) are deferred — the three paths above cover the current need.
