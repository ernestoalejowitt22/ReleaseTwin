# data-export Specification

## Purpose
Lets an organization pull its full run history and stored evidence out of the
hosted platform, at any time, in one documented and self-describing archive — the
continuity commitment made on the marketing security page and in
`docs/continuity.md`, backed by an actual endpoint.
## Requirements
### Requirement: An organization admin can export the organization's run history and evidence
An admin of an organization SHALL be able to obtain, in a single request, an
archive containing every uploaded case report and flag-proof report across every
project in that organization, and every stored evidence document with its
screenshots. The archive SHALL be generated on demand from current stored data,
not a pre-built snapshot.

#### Scenario: Export contains the full run history
- **WHEN** an admin requests an export for an organization with several projects, each holding case and flag-proof reports
- **THEN** the archive contains every one of those reports, each identifying its project, with its result, classification, fixture hash, release label, cleanup status, and upload time

#### Scenario: Export contains each stored evidence document and its screenshots
- **WHEN** the organization has reports with uploaded evidence documents
- **THEN** the archive contains each evidence document exactly as it was redacted by the customer's CLI before upload, alongside the screenshot images it references

#### Scenario: Reports without evidence still appear
- **WHEN** a report was uploaded without an evidence document (metadata only)
- **THEN** it still appears in the run history portion of the archive, with no evidence document or screenshots for it

### Requirement: The export is admin-gated and organization-scoped
Exporting SHALL require the `admin` role in the organization being exported.
A `member`, a `viewer`, or a user with no membership in that organization SHALL
be refused. The archive SHALL contain data belonging only to that one
organization.

#### Scenario: A non-admin member is refused
- **WHEN** a user whose role is `member` or `viewer` requests an export
- **THEN** the request is rejected as forbidden and no archive is produced

#### Scenario: The export never crosses organization boundaries
- **WHEN** an admin of organization A requests an export
- **THEN** the archive contains no report, evidence document, or screenshot belonging to any other organization, and no organization, project, or user identifier for any other organization

### Requirement: The archive is self-describing and its format is documented
The archive SHALL include a manifest identifying the format version, the
organization it was generated for, the generation timestamp, and an inventory of
its contents. The archive layout and every field SHALL be documented so a
customer can consume it with their own tools and no ReleaseTwin-specific
knowledge or transformation.

#### Scenario: The manifest is present
- **WHEN** an export archive is opened
- **THEN** it contains a manifest naming a format version, the source organization, the generation time, and counts of reports and evidence documents included

#### Scenario: The format is externally consumable
- **WHEN** a customer reads the published format documentation and opens an archive
- **THEN** every file and field is accounted for by the documentation, with no proprietary encoding to reverse

### Requirement: The export carries no credentials or fixture content
The archive SHALL contain only the metadata the ingest contract already accepts
and evidence documents already redacted by the customer — never fixture file
contents, request or response bodies beyond what a redacted evidence document
holds, API tokens, adapter credentials, project secrets, or Merchant-of-Record
identifiers.

#### Scenario: No secret-shaped data is present
- **WHEN** an export archive is inspected
- **THEN** it contains no value stored as an API token, an adapter credential, a project secret, or a billing customer/subscription identifier

