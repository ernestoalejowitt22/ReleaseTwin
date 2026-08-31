## ADDED Requirements

### Requirement: Trend series are computed from existing report metadata
The system SHALL compute, for a project and for an organization, time-bucketed series over
a selectable window (7, 30, or 90 days): case pass rate, flag-proof pass rate, run volume,
and a failure-classification breakdown. The computation SHALL use only data already
present on uploaded case and flag-proof reports (outcome, classification, flag-proof
result, upload time) — it SHALL NOT require any new uploaded field.

#### Scenario: A project's 30-day trend is returned
- **WHEN** an authenticated customer requests the 30-day trend for one of their projects
- **THEN** the response contains daily buckets covering the window, each with the case
  pass rate, flag-proof pass rate, run volume, and classification counts for that day

#### Scenario: Buckets with no runs are present as zero points
- **WHEN** a day in the requested window has no uploaded reports
- **THEN** that day still appears in the series with a run volume of zero and null rates

#### Scenario: A rate with no eligible runs is null, not zero
- **WHEN** a bucket has case reports but no flag-proof-eligible runs
- **THEN** the flag-proof pass rate for that bucket is null (a gap), not 0%

#### Scenario: 90-day window uses weekly buckets
- **WHEN** the requested window is 90 days
- **THEN** the series is bucketed by week rather than by day

### Requirement: A flakiest-cases list is returned for the window
Alongside the series, the system SHALL return a short list of the cases whose pass/fail
outcome changed most often within the window, each with its flip count. A case whose
outcome never changed in the window SHALL NOT appear.

#### Scenario: A case that alternates pass/fail ranks above a stable one
- **WHEN** case A alternated pass and fail several times in the window and case B always
  passed
- **THEN** case A appears in the flakiest list with its flip count and case B does not

### Requirement: The organization rollup aggregates across all of the org's projects
The system SHALL provide an organization-level trend that aggregates the same series
across every project in the caller's organization, scoped so a caller sees only their own
organization's data.

#### Scenario: Rollup covers every project
- **WHEN** an organization with three projects requests its organization trend
- **THEN** each bucket's volume and rates reflect reports from all three projects

### Requirement: Trend endpoints require the trendAnalytics entitlement
Access to the trend endpoints SHALL require the caller's organization to hold the
`trendAnalytics` entitlement, decided through the entitlement service. A caller without it
SHALL receive the standard entitlement-required response and no series data.

#### Scenario: An unentitled organization is refused
- **WHEN** an organization without the `trendAnalytics` entitlement requests any trend
- **THEN** the request is refused with the entitlement-required response and no trend data
  is returned
