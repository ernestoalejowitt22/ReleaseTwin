## MODIFIED Requirements

### Requirement: Evidence storage requires a Paid-tier organization
Accepting and storing uploaded evidence SHALL require the uploading token's organization to
hold the `evidenceViewer` entitlement (granted by the `Team` and `Enterprise` tiers in the
current catalog). The decision SHALL be made through the entitlement service, not by
comparing the organization's tier value directly. An upload from an organization without
that entitlement SHALL have its metadata report stored as normal and its evidence
rejected, with a distinct, non-fatal signal to the uploader.

#### Scenario: Free-tier evidence upload is rejected without failing the report
- **WHEN** an organization without the `evidenceViewer` entitlement uploads a report with
  an evidence document
- **THEN** the metadata report is stored, the evidence is not stored, and the response
  indicates evidence was not accepted

#### Scenario: Paid-tier evidence upload is stored
- **WHEN** an organization with the `evidenceViewer` entitlement uploads a report with an
  evidence document
- **THEN** both the metadata report and the evidence are stored

### Requirement: Each project has a configurable evidence retention window
Every project SHALL have an evidence retention window, expressed in days, with a
system-defined default. The maximum a customer may set SHALL be the organization tier's
`maxEvidenceRetentionDays` entitlement; when that entitlement is unbounded ("custom"), the
maximum SHALL be the system-wide ceiling of 365 days. A signed-in customer SHALL be able
to set the window for a project to any value up to that maximum. A value above the maximum
SHALL be rejected with a message naming the applicable limit.

#### Scenario: Default applies until changed
- **WHEN** a project has never had its retention window set
- **THEN** the system-defined default window is in effect for that project

#### Scenario: Customer lowers the window
- **WHEN** a customer sets a project's retention window to 14 days
- **THEN** evidence for that project is retained for 14 days from upload

#### Scenario: A window above the maximum is rejected
- **WHEN** a customer attempts to set a retention window longer than their tier's
  `maxEvidenceRetentionDays` entitlement
- **THEN** the change is rejected, the response names the tier limit, and the previous
  window remains in effect

#### Scenario: A tier downgrade does not retroactively purge evidence
- **WHEN** an organization moves to a tier with a lower `maxEvidenceRetentionDays` than a
  project's current window
- **THEN** the project's stored window is unchanged until the customer next edits it, at
  which point it is clamped to the new maximum; evidence already stored is not deleted as
  a side effect of the downgrade
