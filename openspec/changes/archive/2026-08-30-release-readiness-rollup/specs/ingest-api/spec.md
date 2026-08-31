## ADDED Requirements

### Requirement: Reports may carry an optional release label
The ingest payload's core metadata MAY include an optional `release` string, carried
unchanged from the uploaded case's `release` label. It is a short opaque grouping
identifier — subject to the same "no sensitive content" guarantee as the case identifier
it sits beside — and the API SHALL store it verbatim alongside the report. A payload with
no `release` value SHALL be accepted and stored exactly as before this field existed.

#### Scenario: A report with a release label is stored with it
- **WHEN** a report is uploaded with a `release` value of `4.2`
- **THEN** the stored report carries `release = "4.2"` and it is available to the release
  rollup

#### Scenario: A report with no release label is unchanged
- **WHEN** a report is uploaded with no `release` value
- **THEN** it is accepted and stored identically to the behavior before this field existed

#### Scenario: The release label defines no sensitive field
- **WHEN** the accepted payload schema is inspected
- **THEN** `release` is a plain short string with no capacity to carry fixture content,
  response bodies, or credentials
