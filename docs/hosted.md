# Free CLI vs. paid hosted

The engine in this repo — CLI, execution kernel, adapters — is free and
source-available (AGPL-3.0-only WITH an adapter exception; see
[Licence](../README.md#licence)). It runs entirely in your own infrastructure:
your laptop or your CI runner. No account, no network call to any ReleaseTwin
service, ever, unless you opt in.

The **hosted dashboard** at [releasetwin.com](https://releasetwin.com) is an
optional layer on top. It never runs your tests — execution always stays in
your own infra — it just stores and displays what the CLI reports.

## What each tier gets you

| | Free | Team / Enterprise |
|---|---|---|
| CLI, execution kernel, adapters | ✓ | ✓ |
| Projects | 1 | Unlimited |
| Uploaded run history + evidence viewer | ✓ | ✓ |
| Evidence retention | 30 days | 12 months (Enterprise: custom) |
| CI integration, run notifications, shareable evidence links | — | ✓ |
| Custom redaction rules, hosted project secrets | — | ✓ |
| Trend analytics, release roll-up | — | ✓ |
| SSO, audit log | — | Enterprise only |

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
