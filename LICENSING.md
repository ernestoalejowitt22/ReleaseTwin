# Licensing

ReleaseTwin is **open core**. Three licenses apply, by directory.

| Path | License | |
|---|---|---|
| `src/**`, `tests/**`, repo-root build glue (`ReleaseTwin.sln`, `Dockerfile`, CI) | **AGPL-3.0** (`LICENSE`) + the **Adapter Linking Exception** (`LICENSE.EXCEPTIONS`) | the engine |
| `examples/**` | **Apache-2.0** (`examples/LICENSE`) | starter cases/fixtures — meant to be copied |
| `hosted/**`, `web/**` | **Business Source License 1.1** (`hosted/LICENSE`, identical at `web/LICENSE`) | the hosted platform |
| `docs/**` | AGPL-3.0 as above; prose may also be reused under CC-BY-4.0 with attribution | documentation |

## The engine — AGPL-3.0 (`src/`, `tests/`)

`ReleaseTwin.Core` (the execution kernel: pipelines, fixture integrity,
prerequisites, cleanup, retry/timeout, failure classification, **flag proof**),
`ReleaseTwin.AdapterSdk`, `ReleaseTwin.Cli`, and the bundled adapters
(Azure DevOps, HTTP, LaunchDarkly, UI) are licensed under the
[GNU Affero General Public License v3.0](./LICENSE).

What this means:

- **Run it, self-host it, fork it, modify it** — freely, for any purpose,
  including in production and inside a company.
- **If you run a modified version as a network service for others**, AGPL-3.0
  §13 requires you to offer those users the corresponding source of your
  modified version. Running the *unmodified* engine as part of your own CI is
  not "offering it to others" and triggers nothing.
- A contribution to these paths is accepted under AGPL-3.0.

### The Adapter Linking Exception

Writing an **adapter** — a module that plugs into `ReleaseTwin.AdapterSdk` /
`ReleaseTwin.Core` extension points to add operations, prerequisites, cleanup,
feature-state control, or evidence — does **not** force your adapter to be
AGPL-3.0. Under [`LICENSE.EXCEPTIONS`](./LICENSE.EXCEPTIONS) you may release an
independent adapter under any OSI-approved or proprietary license, even though
it links the AGPL engine at runtime. The engine itself, and any modification to
it, stay AGPL-3.0.

(This is why there is no separate permissive license file inside
`src/ReleaseTwin.AdapterSdk/` — the exception, not a conflicting license on a
project that links AGPL code, is what keeps adapter authors free.)

## The examples — Apache-2.0

`examples/` is [Apache-2.0](./examples/LICENSE) so that `releasetwin init` can
copy a starter case and fixture into **your** project without attaching a
copyleft license to your test suite. Copy and adapt them freely.

## The hosted platform — BSL 1.1

`hosted/` (`ReleaseTwin.Hosted.Api` + infra) and `web/` (dashboard + marketing
site) are under the [Business Source License 1.1](./hosted/LICENSE):

- **You may** read, modify, and run it for any purpose **except** offering it —
  or a modified version — to third parties as a hosted, managed, or otherwise
  commercial service. Internal use, evaluation, development, and self-hosting
  for your own organization are permitted.
- On the **Change Date** — four years after each version is first published —
  that version converts to **Apache-2.0**.

## Why this split

- The engine is the reusable, forkable value and the differentiator. AGPL keeps
  it genuinely open — anyone can self-host or fork — while ensuring a competitor
  who turns a *modified* engine into a rival service must open their work.
  Permissive licensing here would give that away for nothing.
- Adapters are an ecosystem play; the linking exception removes the one reason
  an author might hesitate.
- Examples must be permissive or the scaffold would poison user repos.
- The hosted platform is the commercial surface. BSL keeps it source-available
  and time-bombs to Apache-2.0, so it is never a true black box, while giving a
  small maintainer a defensible position in the interim.

Keeping all three in one repo (rather than a private `hosted/` repo) is a
deliberate operational simplification.

## Contributions

See [CONTRIBUTING.md](./CONTRIBUTING.md). Contributions are accepted under the
license of the path they touch (AGPL-3.0 for the engine, Apache-2.0 for
`examples/`, BSL 1.1 for `hosted/`+`web/`), with a DCO sign-off.

## Trademarks

"ReleaseTwin" (and the provisional "Validuo") are not licensed for use as your
own product or service name by any license above.
