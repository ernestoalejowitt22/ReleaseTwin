## Purpose

Lets a run's pass/fail evidence reach the ticket a case's `oracle.locator`
names — GitHub Issues, Bitbucket issues, or Azure Boards work items — so a
ticket-centric review process sees the same proof a PR-centric one already
gets from `ci-pr-integration`.

## ADDED Requirements

### Requirement: Locator convention per tracker
A case's `oracle.locator` SHALL be recognized as a write-back target when it
matches one of the supported trackers' key conventions (a bare issue number
such as `#123`, or a work-item ID) resolved against the same repository or
project the CI run is already scoped to. A locator that does not match any
supported convention SHALL be left exactly as-is with no write-back attempt,
and SHALL NOT be treated as a case or run failure.

#### Scenario: Recognized locator triggers write-back
- **WHEN** a case's `oracle.locator` is `#42` and the run's CI integration is
  scoped to a specific GitHub repository
- **THEN** the system attempts to post evidence to issue 42 in that
  repository

#### Scenario: Freeform locator is skipped, not failed
- **WHEN** a case's `oracle.locator` is a freeform string that matches no
  supported tracker convention (e.g. "see design doc")
- **THEN** the system does not attempt any write-back for that case and the
  case's pass/fail outcome is unaffected

### Requirement: Evidence comment content
A ticket write-back comment SHALL include the run's overall verdict, the
case ID and oracle locator it was generated for, the pass/fail outcome, and a
link to the run's evidence (the same evidence link already produced for
`ci-pr-integration` or `evidence-sharing`, as available). It SHALL NOT
include fixture contents or any other case data beyond what the existing PR/MR
comment already exposes.

#### Scenario: Comment posted for a failing case
- **WHEN** a case linked to a resolvable ticket fails
- **THEN** the ticket receives a comment naming the case ID, the failure
  outcome, and a link to the run's evidence

### Requirement: Opt-in, per-integration
Ticket write-back SHALL be disabled by default and SHALL only run when
explicitly enabled for a given CI integration invocation. Enabling it SHALL
NOT require any credential beyond what that CI integration already needs to
authenticate to its own platform (e.g. the GitHub Action's existing token
scope, the Bitbucket Pipe's existing repository access).

#### Scenario: Write-back does not run unless enabled
- **WHEN** a CI integration runs without ticket write-back explicitly enabled
- **THEN** no write-back attempt is made, regardless of any case's
  `oracle.locator`

### Requirement: Write-back failure does not fail the run
If posting to a ticket fails (the ticket doesn't exist, the credential lacks
permission, the tracker API is unreachable), the system SHALL record that
failure in the run's own logs/output but SHALL NOT change the case's or run's
pass/fail outcome because of it.

#### Scenario: Ticket API error is non-fatal
- **WHEN** a resolvable locator's write-back attempt receives a 404 or 403
  from the tracker's API
- **THEN** the run's logs note the failed write-back and the case's own
  pass/fail outcome is unchanged
