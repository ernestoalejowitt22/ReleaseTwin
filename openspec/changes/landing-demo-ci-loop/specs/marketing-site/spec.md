## ADDED Requirements

### Requirement: The landing page leads with the merge-gate → evidence loop
The public landing page SHALL present, as its primary product demo, the loop from a
pull-request merge gate through the rendered PR verdict to redacted evidence on the
hosted dashboard. It SHALL NOT contain a page-local, hand-authored run-history table
standing in for the dashboard.

The demo SHALL include distinct panels for:
- the `ReleaseTwin` check on a pull request in a failing, merge-blocking state;
- the same check passing after a fix;
- the rendered PR comment (totals, flag-proof verdict, notable-cases table);
- the hosted dashboard, covering at least run history and the evidence viewer with auth
  headers and credential-shaped fields stripped. Trend analytics and the release-readiness
  rollup MAY be shown when there is representative data to render them convincingly.

Each panel SHALL carry a caption stating the claim it demonstrates. The existing animated
terminal recording MAY remain on the page as a supporting "under the hood" element but
SHALL NOT be the primary demo.

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
  verdict is readable; evidence leaves as metadata only; execution runs in your runner)

### Requirement: The site documents CI portability beyond GitHub
The site SHALL state that the machine-readable run summary (`--summary-json`) is
CI-agnostic and SHALL include a Bitbucket Pipelines configuration snippet that runs the
CLI as a merge gate. It SHALL NOT present a Bitbucket screenshot or claim a packaged
Bitbucket integration exists while none does.

#### Scenario: Bitbucket users see a starting point
- **WHEN** a visitor using Bitbucket reads the CI documentation
- **THEN** they find a Pipelines YAML snippet and a note that the summary contract is not
  GitHub-specific
