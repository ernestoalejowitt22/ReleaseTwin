# Free CLI vs. paid hosted

The engine in this repo — CLI, execution kernel, adapters — is free and
source-available (AGPL-3.0-only WITH an adapter exception; see
[Licence](../README.md#licence)). It runs entirely in your own infrastructure:
your laptop or your CI runner. No account, no network call to any ReleaseTwin
service, ever, unless you opt in.

The **hosted dashboard** at [releasetwin.com](https://releasetwin.com) is an
optional layer on top. By default it never runs your tests — execution stays in
your own infra and it just stores and displays what the CLI reports. The one
opt-in exception is hosted evidence runners, a paid feature that executes a
pinned journey on ReleaseTwin-operated compute.

## What each tier gets you

| | Free | Team / Enterprise |
|---|---|---|
| CLI, execution kernel, adapters | ✓ | ✓ |
| CI integration (GitHub Action, Bitbucket Pipe, GitLab Component) | ✓ | ✓ |
| Projects | 1 | Unlimited |
| Uploaded run history + evidence viewer | ✓ | ✓ |
| Evidence retention | 7 days | 12 months (Enterprise: custom) |
| 14-day Team trial, starts with your first real run | ✓ | — |
| Run notifications, shareable evidence links | — | ✓ |
| Hosted project secrets | — | ✓ |
| Trend analytics, release roll-up, regression diff, flag blast radius, merge gate | — | ✓ |
| Ticket-tracker write-back (Jira, Linear, GitHub, Bitbucket, Azure Boards) | — | ✓ |
| Programmatic API | — | ✓ |
| SSO, audit log, flag-rollout re-verification | — | Enterprise only (planned — not built yet; see releasetwin.com/features) |

Redaction — the built-in credential denylist plus your own per-case allow/deny rules — runs in the
CLI before anything is uploaded, on every tier.

Full feature matrix and current pricing:
[releasetwin.com/pricing](https://releasetwin.com/pricing).

## What gets uploaded

Only if you set an API token does the CLI talk to the hosted API at all. By
default it uploads report *metadata* only — case ID, oracle reference, fixture
hash, pass/fail, classification — never fixture content, response bodies, or
secrets. The optional evidence document (screenshots, redacted request/response
text) is opt-in per project and is redacted locally by the CLI before upload;
the hosted API stores it opaquely without inspecting it. See
[docs/installation-model.md](installation-model.md) for the full trust-boundary
detail.

## If the hosted service ever goes away

The CLI and execution kernel are open source and run entirely in your own
infra — a hosted outage never blocks a release. See
[docs/continuity.md](continuity.md) for the continuity commitment.
