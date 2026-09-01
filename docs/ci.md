<!--
SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
SPDX-License-Identifier: AGPL-3.0-only WITH LicenseRef-ReleaseTwin-Adapter-Exception
-->

# ReleaseTwin in CI

The CLI exits non-zero on any case failure, so it drops into any pipeline as a required
check with no extra wiring. Three ways to get the CLI into a job:

```yaml
name: Release-proof gate
on: pull_request
jobs:
  release-proof:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      # A — the container image (no .NET on the runner)
      - run: docker run --rm -v "$PWD:/workspace:ro"
          ghcr.io/ernestoalejowitt22/releasetwin/cli:0.2.0 /workspace/cases

      # B — the .NET global tool (the runner has .NET)
      # - run: dotnet tool install -g releasetwin --version 0.2.0
      # - run: releasetwin ./cases

      # C — the GitHub Action (adds a PR comment + check run) — see "PR annotations" below
```

Pin a released version (`cli:0.2.0`, `--version 0.2.0`, `@v0.2.0`) in CI. A non-zero exit
fails the job, fails the check, blocks the merge — the same gate you trust for unit tests.

## Machine-readable run summary

Pass `--summary-json <path>` (or set `RELEASETWIN_SUMMARY_JSON`) and the CLI writes a
versioned JSON summary of the run after it finishes — on pass or fail — alongside its
normal human output:

```jsonc
{
  "schemaVersion": 1,
  "overall": "failed",
  "totals": { "passed": 12, "failed": 1, "cases": 13 },
  "flagProof": { "proven": 3, "ineligible": 1, "regressed": 0 },
  "cases": [
    { "id": "HTTP-DEMO-1", "outcome": "passed", "classification": null, "flagProof": null, "release": "4.2" },
    { "id": "CLM-042", "outcome": "failed", "classification": "infrastructure", "flagProof": null, "release": null }
  ]
}
```

It carries only metadata the CLI already prints — ids, outcomes, classifications,
flag-proof results, and the `release` label. No bodies, no secrets. With no flag set, no
file is written and behavior is unchanged.

## PR annotations

`integrations/github-action/` is an open-source (Apache-2.0) GitHub Action that runs the
CLI with `--summary-json` and renders the summary onto the pull request:

- a **comment** — created once, updated in place on every re-run (keyed by a hidden
  marker) — with the totals, the flag-proof verdict, and a table of the notable cases
- a **check run** named `ReleaseTwin` with the same outcome, which you can make a required
  status check to block the merge

It uses only the workflow's `GITHUB_TOKEN` and GitHub's REST API — no ReleaseTwin account,
no hosted call.

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
      - uses: ernestoalejowitt22/ReleaseTwin/integrations/github-action@v0.2.0
        with:
          cases-path: cases
          image: ghcr.io/ernestoalejowitt22/releasetwin/cli:0.2.0
```

Pin a full version (`@v0.2.0`) in CI. `@v0` is a floating tag that tracks the latest 0.x
release if you want patches automatically. The `image` input must be a publicly pullable
tag.

**Run-only gate** (no PR comment, just the check): pass `comment: false`. The `ReleaseTwin`
check run still reports pass/fail — make it a required status check on the protected branch
and that's your merge gate, no comment noise.

See [`integrations/github-action/README.md`](../integrations/github-action/README.md) for
every input. This repo dogfoods the Action in `.github/workflows/pr-annotations.yml`.

## Credentials

- HTTP-only cases need nothing.
- A flag-proof leg needs its flag source's credentials as job env
  (`LAUNCHDARKLY_API_TOKEN`, the `AZDO_*` set, …); pass them via the Action's `env-vars` or
  `env-file` input.
- To also land run history + evidence on the hosted dashboard, set `RELEASETWIN_API_TOKEN`
  and `RELEASETWIN_API_URL`.
