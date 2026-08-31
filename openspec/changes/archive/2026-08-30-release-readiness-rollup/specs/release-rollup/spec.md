## ADDED Requirements

### Requirement: A project's releases are listed from its reports
The system SHALL list the distinct `release` labels that appear on a project's uploaded
case and flag-proof reports. A project whose reports carry no `release` label SHALL return
an empty list, not an error.

#### Scenario: Distinct labels are returned
- **WHEN** a project has reports labelled `4.1` and `4.2`
- **THEN** the releases list contains `4.1` and `4.2`

### Requirement: A release rollup summarizes readiness across its cases
For a given project and release label, the system SHALL return, for every case that has
reported under that label, that case's latest result determined from the most recent case
or flag-proof report for that case within the request's recency window (default 14 days).
A flag-proof `proven` result and a plain `passed` result both count as green; a `failed`
result counts as failing; a case whose most recent report is older than the window, or
whose latest flag-proof result is `ineligible`, counts as stale.

The rollup SHALL include counts of green, failing, and stale cases, and a single headline
state:
- **Proven** — every case is green
- **Not proven** — at least one case is failing
- **Incomplete** — no case is failing but at least one is stale (or the release has no
  case with a recent run)

#### Scenario: All green yields Proven
- **WHEN** every case under the release has a recent passing or proven result
- **THEN** the headline state is Proven

#### Scenario: One failing case yields Not proven
- **WHEN** one case under the release has a recent failing result and the rest are green
- **THEN** the headline state is Not proven and the failing count is 1

#### Scenario: A stale case with no failures yields Incomplete
- **WHEN** no case under the release is failing but one case's most recent report is older
  than the recency window
- **THEN** the headline state is Incomplete and that case is counted as stale

#### Scenario: Latest result wins
- **WHEN** a case reported `failed` and then later reported `passed` under the same release
- **THEN** the rollup shows that case as green

### Requirement: Release rollup requires the releaseRollup entitlement
The releases list and rollup endpoints SHALL require the caller's organization to hold the
`releaseRollup` entitlement, decided through the entitlement service, and SHALL be scoped
to the caller's organization. A caller without the entitlement SHALL receive the standard
entitlement-required response.

#### Scenario: An unentitled organization is refused
- **WHEN** an organization without `releaseRollup` requests a release rollup
- **THEN** the request is refused with the entitlement-required response
