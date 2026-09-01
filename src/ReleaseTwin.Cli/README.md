<!--
SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
SPDX-License-Identifier: AGPL-3.0-only WITH LicenseRef-ReleaseTwin-Adapter-Exception
-->

# ReleaseTwin CLI

Release-proof testing for integration-heavy, feature-flagged systems. Compose
HTTP and UI journeys, run them from your own machine or CI, and prove a fix
works by running the same case known-bad and known-good ("flag proof"). Test
data never leaves your infrastructure.

## Install

```
dotnet tool install --global releasetwin
```

Needs the .NET runtime. If you'd rather not install .NET, use the container
image instead — see [docs/install.md](https://github.com/ernestoalejowitt22/ReleaseTwin/blob/main/docs/install.md).

## Run

```
releasetwin ./cases
```

The CLI exits non-zero on any case failure, so it drops into a pipeline as a
required check with no extra wiring. Fixtures resolve from a `fixtures/`
directory that is a sibling of the cases directory. `${ENV_VAR}` references in
case files are interpolated from the process environment. This is identical to
the container image's behavior.

UI-journey cases drive a real browser via Playwright — run `playwright install`
once. HTTP and flag-proof cases need nothing extra.

## More

- [Quickstart](https://github.com/ernestoalejowitt22/ReleaseTwin/blob/main/docs/quickstart.md)
- [Running in CI](https://github.com/ernestoalejowitt22/ReleaseTwin/blob/main/docs/ci.md)
- [All install paths](https://github.com/ernestoalejowitt22/ReleaseTwin/blob/main/docs/install.md)

Licensed AGPL-3.0. Independent adapters that link the AdapterSdk may carry any
license — see [`LICENSE.EXCEPTIONS`](https://github.com/ernestoalejowitt22/ReleaseTwin/blob/main/LICENSE.EXCEPTIONS).
