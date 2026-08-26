## Purpose

Judges, from a project's own upload history, whether that project has gone quiet compared to its
own normal cadence — distinct from a project that simply never had a token configured.

## ADDED Requirements

### Requirement: A project needs a minimum upload history before staleness applies
A project SHALL be judged stale only if it has at least 5 uploads (case reports and flag-proof
reports combined, counted together as one timeline ordered by upload time). A project with fewer
than 5 uploads SHALL never be judged stale, regardless of how long ago its most recent upload (if
any) occurred.

#### Scenario: Too little history to judge
- **WHEN** a project has fewer than 5 uploads total
- **THEN** the project is never judged stale, no matter the time since its last upload

#### Scenario: Enough history to judge
- **WHEN** a project has 5 or more uploads total
- **THEN** the project is eligible to be judged stale or not stale

### Requirement: Staleness is relative to the project's own typical upload gap
For an eligible project, the system SHALL compute the typical gap between consecutive uploads from
that project's own upload history, and SHALL judge the project stale when the time elapsed since
its most recent upload exceeds 3 times that typical gap.

#### Scenario: Upload gap within normal cadence
- **WHEN** an eligible project's time since its last upload is at or below 3 times its typical
  upload gap
- **THEN** the project is not judged stale

#### Scenario: Upload gap exceeds normal cadence
- **WHEN** an eligible project's time since its last upload exceeds 3 times its typical upload gap
- **THEN** the project is judged stale

#### Scenario: Infrequent but steady cadence is not penalized
- **WHEN** an eligible project has historically uploaded roughly once every 30 days, consistently
- **THEN** the project is not judged stale until more than roughly 90 days have passed since its
  last upload
