## ADDED Requirements

### Requirement: The project view presents a release readiness section for entitled organizations
For an organization holding the `releaseRollup` entitlement, the project view SHALL present
a Releases section: the list of release labels seen in that project's reports, and, for a
selected release, its readiness rollup — the headline state (Proven / Not proven /
Incomplete), the green / failing / stale counts, and the per-case latest result. For an
organization without that entitlement, the dashboard SHALL show an upgrade prompt in place
of the Releases section.

#### Scenario: An entitled organization sees the release rollup
- **WHEN** a customer whose organization holds `releaseRollup` opens a project with
  release-labelled reports
- **THEN** the Releases section lists the labels and shows the selected release's headline
  state, counts, and per-case results

#### Scenario: An unentitled organization sees an upgrade prompt
- **WHEN** a customer whose organization lacks `releaseRollup` opens the Releases section
- **THEN** an upgrade prompt is shown in place of the rollup
