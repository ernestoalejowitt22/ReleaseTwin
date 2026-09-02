## MODIFIED Requirements

### Requirement: The landing page leads with the merge-gate → evidence loop

The public landing page SHALL present, as its primary product demo, the loop from a
pull-request merge gate through the rendered PR verdict to redacted evidence on the
hosted dashboard. It SHALL NOT contain a page-local, hand-authored run-history table
standing in for the dashboard.

The landing page SHALL, above that demo, present a problem/value section that names the
failure mode ReleaseTwin catches (for example: API-contract drift, expired sandbox
credentials, or a downstream failure that only appears under real authentication —
discovered after release) and states what a team gains: a required check instead of a
manual pre-release checklist; a readable PR verdict instead of an out-of-band
conversation; linkable redacted evidence instead of terminal scrollback; execution and
test data staying in the team's own runner; and flag-proof that a real credentialed path
executed. The demo panels that follow SHALL be readable as answers to that section.

The demo SHALL include distinct panels for:
- the `ReleaseTwin` check on a pull request in a failing, merge-blocking state;
- the same check passing after a fix;
- the rendered PR comment (totals, flag-proof verdict, notable-cases table);
- at least one non-GitHub CI surface showing the same verdict is not GitHub-specific
  (see "The site documents CI portability beyond GitHub");
- the hosted dashboard, covering at least run history and the evidence viewer with auth
  headers and credential-shaped fields stripped. Trend analytics and the release-readiness
  rollup MAY be shown when there is representative data to render them convincingly.

Each panel SHALL carry a caption stating the claim it demonstrates. The existing animated
terminal recording MAY remain on the page as a supporting "under the hood" element but
SHALL NOT be the primary demo and SHALL NOT sit above the problem/value section.

#### Scenario: The fake dashboard table is gone

- **WHEN** the landing page renders
- **THEN** there is no page-local component enumerating fabricated run rows; the dashboard
  is shown via captured screenshots of the real hosted dashboard

#### Scenario: The merge-gate panels are present

- **WHEN** the landing page renders
- **THEN** it shows the ReleaseTwin PR check in both a failing merge-blocked state and a
  passing state, and the rendered PR comment

#### Scenario: Each demo panel states its claim

- **WHEN** a visitor reads the demo section
- **THEN** every panel has a caption naming what it proves (it is a real merge gate; the
  verdict is readable; evidence leaves as metadata only; execution runs in your runner;
  the verdict is not GitHub-specific)

#### Scenario: The problem/value section precedes the demo

- **WHEN** a visitor scrolls the landing page from the top
- **THEN** they reach a section naming the failure mode and the gains before they reach
  the merge-gate → evidence panels, and the animated terminal recording does not appear
  above it

### Requirement: The site documents CI portability beyond GitHub

The site SHALL state that the machine-readable run summary (`--summary-json`) is
CI-agnostic and SHALL include a Bitbucket Pipelines configuration snippet that runs the
CLI as a merge gate. It SHALL NOT present a Bitbucket screenshot of a pull request or
claim a packaged Bitbucket integration exists while none does.

The landing page's product demo SHALL itself carry a non-GitHub CI panel — the Bitbucket
Pipelines YAML snippet, a generic (non-GitHub, non-PR) pipeline-log render of the CLI
running as a gate, or both — captioned to state that the ReleaseTwin verdict is produced
the same way on any CI from the same `--summary-json` contract. A rendered pipeline log
that is not styled as, or captioned as, a Bitbucket pull request is permitted; a
screenshot of a Bitbucket pull request, or any copy implying a packaged Bitbucket
integration, is not.

#### Scenario: Bitbucket users see a starting point

- **WHEN** a visitor using Bitbucket reads the CI documentation
- **THEN** they find a Pipelines YAML snippet and a note that the summary contract is not
  GitHub-specific

#### Scenario: The landing demo shows portability

- **WHEN** a visitor reads the landing page's product demo
- **THEN** at least one panel shows a non-GitHub CI surface (a Pipelines snippet or a
  generic pipeline-log render) with a caption stating the verdict is identical on any CI

#### Scenario: No packaged Bitbucket integration is implied

- **WHEN** any marketing surface shows or describes Bitbucket
- **THEN** it shows only a configuration snippet or a generic pipeline log, never a
  Bitbucket pull-request screenshot, and never states or implies that a packaged
  Bitbucket app, pipe, or integration exists
