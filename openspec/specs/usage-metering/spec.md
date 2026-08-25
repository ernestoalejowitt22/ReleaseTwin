# usage-metering Specification

## Purpose

Counts and periodizes the volume of uploaded case/flag-proof reports per organization, as pure observability that can exist before any pricing or entitlement decision is made — the foundation any future usage-based billing would meter against.

## Requirements

### Requirement: Uploaded report volume is counted per organization and period
The system SHALL compute a count of uploaded case reports and flag-proof reports, summed across every project belonging to an organization, for the current calendar-month period.

#### Scenario: Reports across multiple projects in the same organization are combined
- **WHEN** an organization owns two or more projects, each with uploaded reports
- **THEN** the computed count for that organization includes reports from all of its projects, not just one

#### Scenario: Reports belonging to a different organization are excluded
- **WHEN** computing the count for one organization
- **THEN** reports uploaded to a project owned by a different organization are never included

#### Scenario: The count reflects only the current period
- **WHEN** an organization has uploaded reports both within and before the current calendar month
- **THEN** the computed count includes only reports uploaded within the current calendar month

### Requirement: Unlinked local CLI runs are not counted
The system SHALL only count reports that were actually uploaded to the ingest API; it SHALL NOT attempt to count or estimate CLI runs that never uploaded (no `RELEASETWIN_API_TOKEN` configured).

#### Scenario: A local-only run does not appear in any count
- **WHEN** a case is run by the CLI without an API token configured
- **THEN** no uploaded-report count anywhere increases as a result, since no upload ever occurred
