## Purpose

Makes a brand-new organization's first session useful instead of empty: it can
see what run history and flag-proof evidence look like before its own first
upload, and it is walked through creating a project, copying a token, and running
the CLI.

## ADDED Requirements

### Requirement: A new organization sees a seeded sample project until its first real ingest
Until an organization has ingested at least one real run, the dashboard SHALL
display one seeded sample project containing representative, clearly-labelled
example data: a short run history, at least one passing and one failing run, one
flag-proof result, and one evidence document drill-down. The sample project SHALL
be visibly marked as an example and SHALL NOT count toward the organization's
plan project limit.

#### Scenario: Empty organization sees the sample
- **WHEN** a member of an organization that has never ingested a run opens the dashboard
- **THEN** a sample project is shown, labelled as an example, with browsable run history, a flag-proof result, and an evidence drill-down

#### Scenario: Sample does not consume the project quota
- **WHEN** a Free-tier organization that still shows the sample project creates its first real project
- **THEN** the creation succeeds and is not blocked by the one-project Free limit

#### Scenario: Sample is retired after the first real run
- **WHEN** an organization ingests its first real run
- **THEN** the sample project is removed from that organization's dashboard and does not reappear

#### Scenario: Sample data is read-only
- **WHEN** a user attempts to issue a token for, upload to, or delete the sample project
- **THEN** the action is refused because the sample project is read-only

### Requirement: A guided first-run panel walks a new organization to its first upload
Until an organization has ingested its first real run, the dashboard SHALL show a
guided panel with the ordered steps to a first upload: create a project, generate
an API token, and run the CLI with that token. The panel SHALL show the exact
command to run, including the API URL and a placeholder for the token, and SHALL
reflect progress as each step is completed.

#### Scenario: Panel shows the next action
- **WHEN** a new organization has created a project but not yet issued a token
- **THEN** the guided panel highlights "generate an API token" as the next step

#### Scenario: Panel shows a copyable run command
- **WHEN** the guided panel is displayed
- **THEN** it shows the CLI command to run including the hosted API URL, with the token shown as a placeholder to substitute

#### Scenario: Panel disappears after activation
- **WHEN** the organization's first real run is ingested
- **THEN** the guided panel is no longer shown
