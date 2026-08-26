## 1. Dedicated single-case example

- [x] 1.1 Add `examples/cases-http-only/example-http.yaml` (copy of `examples/cases/example-http.yaml`),
      confirming it resolves `example-http.json` from the existing `examples/fixtures/` via the
      CLI's default fixtures-root convention.

## 2. Rewrite the Cypress spec

- [x] 2.1 Rewrite `dashboard-staleness-banner.cy.ts`: sign in, create/reuse a project, issue a
      token, run the CLI 5 times against `examples/cases-http-only` a couple seconds apart via the
      existing `runCli` task.
- [x] 2.2 Wait a real, generous multiple of the observed cadence, reload, assert the banner appears.
- [x] 2.3 Run the CLI once more, reload, assert the banner clears.
- [x] 2.4 Run the spec locally end to end; capture a screenshot of the banner appearing.

## 3. Remove the seeding backdoor

- [x] 3.1 Remove the `/dev/seed-case-report-history` endpoint from `Program.cs`.
- [x] 3.2 Run the full `.NET` test suite to confirm nothing referenced the removed endpoint.
