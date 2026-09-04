<!--
SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
SPDX-License-Identifier: Apache-2.0
-->

# ReleaseTwin — GitLab CI/CD Component

**[releasetwin.com](https://releasetwin.com)** — release-proof testing for
integration-heavy, feature-flagged systems.

Runs your ReleaseTwin case suite in a GitLab pipeline and feeds the result into
GitLab's **native test surfaces**:

- the **merge-request test widget** — one row per case, failures and flag-proof
  verdicts surfaced inline on the MR
- the pipeline **Tests** tab — the same, with history and trends

It does this by writing a JUnit report from the CLI and handing it to GitLab as
`artifacts:reports:junit`. **No ReleaseTwin account, and no GitLab API token, is
involved** — execution stays entirely in your CI and the widget is populated by
the artifact alone.

This component is **Apache-2.0** licensed (see `LICENSE`), independently of the
ReleaseTwin engine's copyleft license — fork and adapt it freely.

## Usage

```yaml
include:
  - component: $CI_SERVER_FQDN/releasetwin/releasetwin/releasetwin@0.2.0
    inputs:
      cases-path: cases

stages:
  - test
```

Pin a released version (`@0.2.0`). Until the component is published to your
instance's CI/CD Catalog you can also include it by raw path:

```yaml
include:
  - remote: 'https://raw.githubusercontent.com/ernestoalejowitt22/ReleaseTwin/v0.2.0/integrations/gitlab-component/templates/releasetwin.yml'
```

The generated job runs on the CLI container image, writes `junit.xml` and
`summary.json` as artifacts (`when: always`, so the widget populates even on a
failing run), and **exits non-zero on any case failure or adverse flag-proof
verdict** — make it a required check on your protected branch and that is your
merge gate.

### Outcome mapping

The JUnit widget answers *"is this build release-proven?"* — a flag-proof case
that could not be paired (`Ineligible`, `ControlFailed`, `ControlUnverified`)
shows as a **failure**, deliberately stricter than the CLI's own exit code. The
full flag-proof nuance is in `summary.json` and the job log. See
[`docs/ci.md`](../../docs/ci.md).

## Inputs

| Input | Default | Notes |
| --- | --- | --- |
| `cases-path` | `cases` | Directory of case files, relative to the project root. |
| `image` | pinned digest (release-managed) | CLI container image. Override with your own `…/cli:X.Y.Z` or `…/cli@sha256:…`; must be publicly pullable. |
| `stage` | `test` | Pipeline stage the job runs in. Declare it in your `stages:`. |
| `job-name` | `releasetwin` | Name of the generated job. |

## Credentials

- **HTTP-only cases need nothing.**
- A **flag-proof leg** needs its flag source's credentials as CI/CD variables
  (`LAUNCHDARKLY_API_TOKEN`, the `AZDO_*` set, or a `flag_proof.control` block's
  `${VAR}` references). Set them as
  [project or group CI/CD variables](https://docs.gitlab.com/ci/variables/); the
  job inherits them.
- To also land run history + evidence on the hosted dashboard, set
  `RELEASETWIN_API_TOKEN` and `RELEASETWIN_API_URL` as CI/CD variables.

### Secrets and fork / detached pipelines

Anything you expose to the job as a CI/CD variable is readable by the case files
it runs. **Case files are code.** Do not make ingest tokens or any other
sensitive secret available on a pipeline that runs merge requests from forks or
untrusted contributors — restrict sensitive variables to protected branches and
tags.

## Requirements

- A GitLab Runner using the `docker` or `kubernetes` executor (it runs the job in
  the CLI container image).
- GitLab 16.0+ for CI/CD Components; the `remote:` include form works on any
  version with a shared runner.
