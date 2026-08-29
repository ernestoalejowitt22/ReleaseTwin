## ADDED Requirements

### Requirement: A report's uploaded evidence can be viewed in detail
When a case or flag-proof report has stored evidence, the dashboard SHALL let the customer open a detail view for that report showing the redacted step-by-step evidence: each executed step in order with its outcome, duration, and any adapter-emitted evidence, and for assertion steps the checked expression, expected value, and observed value. For a flag-proof report, the known-bad and known-good legs SHALL be shown as distinct sections.

#### Scenario: Evidence detail opens from a report
- **WHEN** a customer opens the detail view of a report that has stored evidence
- **THEN** the redacted per-step evidence for that run is displayed in pipeline order

#### Scenario: Report without evidence offers no detail view
- **WHEN** a report has no stored evidence (never uploaded, or purged, or the organization is not entitled)
- **THEN** the dashboard shows the report in run history without an evidence detail view, indicating why evidence is unavailable

#### Scenario: Flag-proof legs are shown separately
- **WHEN** a customer opens the evidence detail view of a flag-proof report
- **THEN** the known-bad leg and known-good leg evidence are presented as distinct sections

### Requirement: The evidence detail view states that redaction happened before upload
The evidence detail view SHALL make visible that the evidence was redacted by the customer's own CLI before upload, and SHALL label any screenshot evidence as best-effort-redacted.

#### Scenario: Redaction provenance is shown
- **WHEN** the evidence detail view is displayed
- **THEN** it states that redaction was applied in the customer's CLI before upload

#### Scenario: Screenshots are labelled best-effort
- **WHEN** the evidence detail view shows a screenshot
- **THEN** that screenshot is labelled as best-effort-redacted

### Requirement: A customer controls evidence capture and retention per project
The dashboard SHALL provide, for a Paid-tier organization, a per-project setting to enable or disable the evidence-capture default for that project's CLI runs, and to set that project's evidence retention window up to the system maximum. The setting SHALL show the retention window currently in effect. A Free-tier organization SHALL see this control as unavailable with the tier reason given.

#### Scenario: Paid-tier customer enables capture and sets retention
- **WHEN** a Paid-tier customer enables evidence capture for a project and sets its retention window to a value at or below the maximum
- **THEN** the setting is saved, becomes the hosted per-project default the CLI reads, and the chosen window governs that project's evidence purge

#### Scenario: Retention above the maximum is refused in the UI
- **WHEN** a customer tries to set a retention window above the system maximum
- **THEN** the dashboard refuses the value and keeps the prior window

#### Scenario: Free-tier customer cannot enable capture
- **WHEN** a Free-tier customer views the evidence settings for a project
- **THEN** the control is shown as unavailable with the plan-tier reason, and no evidence default is set

### Requirement: The journey builder can author evidence redaction rules
The visual journey builder SHALL let a customer add evidence redaction rules — an allowlist of paths/fields to keep and a denylist of headers, JSONPaths, fields, selectors, or regions to mask — and SHALL include them in the saved journey version so a CLI run of that journey applies the same redaction a hand-written case file's `evidence:` block would.

#### Scenario: Saved journey carries the redaction rules
- **WHEN** a customer adds an allowlist path and a denylist rule in the builder and saves
- **THEN** the saved journey version's content includes an evidence block expressing exactly those rules

#### Scenario: No rules means no evidence block
- **WHEN** a customer saves a journey without adding any evidence redaction rule
- **THEN** the saved journey version contains no evidence block, and a CLI run applies only the built-in redaction

### Requirement: Evidence is shown only within the uploading organization
The dashboard SHALL display stored evidence only to members of the organization that uploaded it, under every view and filter — the same scoping guarantee that applies to reports.

#### Scenario: Evidence is never shown cross-organization
- **WHEN** a customer views any dashboard view
- **THEN** no evidence belonging to another organization is displayed
