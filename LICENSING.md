# Licensing

ReleaseTwin is **open core**. Two licenses apply, by directory.

## Apache License 2.0 — the engine

Everything except `hosted/` and `web/` is licensed under
[Apache-2.0](./LICENSE). This is the part you run in your own infrastructure:

| Path | What it is |
|---|---|
| `src/**` | `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, the CLI, and all adapters |
| `tests/**` | the test suite for the above |
| `examples/**` | runnable example cases and fixtures |
| `docs/**` | documentation |
| repo root (`ReleaseTwin.sln`, `Dockerfile`, CI config, this file) | build + project glue |

Use it, fork it, embed it, run it in production, build your own adapters and
distribution around it — the Apache-2.0 grant applies, including its patent
grant. There is no "open core catch" on the engine.

## Business Source License 1.1 — the hosted platform

`hosted/**` (`ReleaseTwin.Hosted.Api` and its infrastructure) and `web/**`
(the dashboard and marketing site) are licensed under the
[Business Source License 1.1](./hosted/LICENSE) (identical copy at
[`web/LICENSE`](./web/LICENSE)).

In plain terms:

- **You may** read the source, modify it, and run it for any purpose that is
  **not** offering it to third parties as a hosted or managed commercial
  service — internal use, evaluation, development, self-hosting for your own
  team, and contributing back are all fine.
- **You may not**, without a separate commercial agreement, operate `hosted/`
  or `web/` (or a modified version) as a competing commercial product or
  service for others.
- On the **Change Date** — four years after each version is first published —
  that version automatically converts to **Apache-2.0**.

See the license file for the exact Additional Use Grant, Change Date, and
Change License parameters.

## Why split it this way

The engine is where the reusable, forkable value is, and locking it down would
undercut the whole point of shipping it. The hosted platform is the commercial
surface; BSL keeps it source-available and time-bombs to Apache-2.0 so it is
never a true black box, while giving a solo maintainer a defensible commercial
position in the interim. Keeping both in one repo (rather than splitting
`hosted/` into a private repo) is a deliberate operational simplification.

## Contributions

Contributions to Apache-2.0 paths are accepted under Apache-2.0.
Contributions to BSL paths are accepted under the BSL 1.1 as stated in that
license's contribution terms. See [CONTRIBUTING.md](./CONTRIBUTING.md).

## Trademarks

"ReleaseTwin" (and the provisional "Validuo") are not licensed for use as your
own product or service name by either license above. See the Apache-2.0
trademark clause (§6).
