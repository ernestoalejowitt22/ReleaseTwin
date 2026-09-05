<!--
SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
SPDX-License-Identifier: Apache-2.0
-->

# ReleaseTwin — Bitbucket Pipe

**[releasetwin.com](https://releasetwin.com)** — release-proof testing for
integration-heavy, feature-flagged systems.

Runs your ReleaseTwin case suite in a Bitbucket Pipelines step and writes a JUnit XML
report to the path Bitbucket already scans for test results — **no `artifacts:`
configuration, no ReleaseTwin account, and no Bitbucket API token required.**
Execution stays entirely in your pipeline.

This pipe is **Apache-2.0** licensed (see `LICENSE`), independently of the
ReleaseTwin engine's copyleft license — fork and adapt it freely.

> **On GitHub?** Use [`integrations/github-action/`](../github-action/) for a PR
> comment and check run. **On GitLab?** Use
> [`integrations/gitlab-component/`](../gitlab-component/) for the native
> merge-request test widget.

## Usage

```yaml
pipelines:
  pull-requests:
    '**':
      - step:
          name: Release-proof gate
          script:
            - pipe: docker://ghcr.io/ernestoalejowitt22/releasetwin/bitbucket-pipe:0.2.0
              variables:
                CASES_PATH: 'cases'
```

Bitbucket Pipelines collects test results from any `**/test-results/*.xml` (or
`**/junit.xml`) file it finds after a step, so the pipe's default
`RELEASETWIN_JUNIT_XML` path needs no further wiring. A case failure or an adverse
flag-proof verdict fails the step's exit code, so this can be a required check on
the target branch.

## Inputs

| Variable | Default | Description |
| --- | --- | --- |
| `CASES_PATH` | `cases` | Directory of case files, relative to the pipeline's working directory. |
| `RELEASETWIN_JUNIT_XML` | `test-results/junit.xml` | Where the pipe writes the JUnit report. |
| `RELEASETWIN_SUMMARY_JSON` | — | Optional path to also write a machine-readable JSON run summary. |

Flag-source credentials, a hosted API token, or any other `${ENV_VAR}` a case file
references are forwarded the same way any Bitbucket Pipelines variable is — declare
them on the repository/workspace and reference them from the step, same as the raw
`image:`/`script:` form below.

## Why a wrapper image

The published `releasetwin/cli` image takes its cases directory as a positional
argument, which a Bitbucket Pipe's `variables:` (environment variables) can't set
directly. This image is a thin wrapper — one entrypoint script — that forwards
`CASES_PATH` as that argument; every other setting (`RELEASETWIN_JUNIT_XML`,
`RELEASETWIN_SUMMARY_JSON`, adapter credentials) already works as a plain env var
on the base CLI image and passes through unmodified. See `../../docs/ci.md` for
the raw `image:`/`script:` alternative, which needs no extra image at all.

## Catalog listing

This pipe is usable today via the direct `pipe: docker://...` reference above.
It has not been submitted to Atlassian's `official-pipes` catalog — that's an
optional, separately-pursued step with no bearing on whether the pipe works.
