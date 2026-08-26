## ADDED Requirements

### Requirement: A stale project shows a staleness banner
When the selected project is judged stale, the dashboard SHALL display a banner on that project's
view indicating that its uploads have gone quiet compared to its usual cadence. The banner SHALL
appear alongside the project's existing run history, not in place of it.

#### Scenario: Stale project shows the banner
- **WHEN** the selected project is judged stale
- **THEN** the dashboard displays a staleness banner alongside that project's run history

#### Scenario: Non-stale project shows no banner
- **WHEN** the selected project is not judged stale (including projects too new to judge)
- **THEN** no staleness banner is displayed for that project

#### Scenario: Banner clears once uploads resume
- **WHEN** a project that was judged stale receives a new upload that brings it back within its
  normal cadence
- **THEN** the staleness banner no longer appears for that project
