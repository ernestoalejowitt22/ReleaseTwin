## Context

`UploadStalenessCalculator.IsStale` (see archived `dashboard-upload-staleness`) compares "gap since
last upload" against "3× the median gap between past uploads" — a ratio, not an absolute duration.
Nothing in the rule cares whether that cadence is measured in days or seconds. The existing e2e
suite already has a proven pattern for real CLI uploads: `product-usage-loop.cy.ts` uses the
`runCli` Cypress task (`cypress.config.ts`) to shell out to a real `dotnet run` against a
dashboard-issued token.

## Goals / Non-Goals

**Goals:**
- Prove the staleness banner against real, unmocked uploads — real CLI invocations, real ingest
  calls, real `UploadedAt` timestamps — with zero seeded/backdated data.
- Keep the test's total runtime reasonable (real CLI startup + a short real wait, not minutes).

**Non-Goals:**
- Not testing the actual multi-day cadence a real customer would have — the ratio-based rule makes
  that unnecessary; a compressed real cadence exercises identical logic.
- Not removing the `/dev/seed` endpoint (unrelated dev-walkthrough tool) — only
  `/dev/seed-case-report-history`, added solely for the now-replaced seeding approach.

## Decisions

**Compress the cadence to seconds, not days, and wait in real time.** Run the CLI 5 times against
`examples/cases-http-only` (one real upload each), a couple seconds apart, establishing a median gap
of roughly that size. Then `cy.wait()` a real ~3× that gap before reloading and asserting the banner
appears — no backdating, no mocked clock. Run the CLI once more and reload to assert the banner
clears (the same "banner clears once uploads resume" scenario the original test covered).

**A dedicated single-case example directory, not `examples/cases`.** The bundled `examples/cases`
uploads two reports per run (`HTTP-DEMO-1` plus `example-claim.yaml`, which fails without Azure
DevOps credentials but still uploads as a case report per `product-usage-loop.cy.ts`'s own note) —
that would make "one upload per CLI run" untrue and complicate the cadence math for no benefit here.
`examples/cases-http-only/example-http.yaml` is a copy of the existing zero-credential HTTP case;
its fixture is resolved from the existing `examples/fixtures/example-http.json` via
`CaseFileLoader`'s default `<casesDirectory>/../fixtures` convention — no fixture duplication.

**Remove `/dev/seed-case-report-history` entirely rather than leaving it unused.** It only ever
existed to support the seeding approach this change replaces; keeping a dev-only backdoor around
after its one caller is gone is dead weight, not a hedge.

## Risks / Trade-offs

- **Real CLI startup time makes this slower than a seeded test.** Each `dotnet run` invocation has
  real process overhead (a few seconds, once already built). → Accepted: this is the same cost
  `product-usage-loop.cy.ts` already pays for the same reason (real, not mocked).
- **A few-second cadence is more sensitive to test-runner jitter than a multi-day one would be in
  production.** A slow CI runner could stretch real gaps between CLI invocations enough to distort
  the intended ~2s cadence. → Mitigate by keeping the multiplier headroom generous (waiting well
  past 3× the observed cadence, not right at the boundary) rather than asserting on a tight margin.

## Migration Plan

None — test-only change plus removal of a dev-only endpoint never reachable outside
`IsDevelopment()`. No effect on any deployed system.
