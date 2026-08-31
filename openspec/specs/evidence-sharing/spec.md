# evidence-sharing Specification

## Purpose
Lets a customer hand one run's evidence document to someone outside their
organization — an auditor, a reviewer, a manager — through a revocable read-only
link, without giving that person an account or exposing anything beyond the
already-redacted evidence view.
## Requirements
### Requirement: An admin can create a share link for a single run
An admin SHALL be able to create a share link scoped to exactly one run. The link
SHALL contain a high-entropy unguessable token. Creating a link SHALL be an
explicit per-run action; there SHALL be no organization-wide or project-wide
share link.

#### Scenario: Admin creates a link for one run
- **WHEN** an admin creates a share link for a specific run
- **THEN** a URL containing an unguessable token is returned, and it grants access to that run only

#### Scenario: Token does not generalize
- **WHEN** a share-link token for run X is altered or used to request run Y
- **THEN** access is denied

### Requirement: A share link renders only the redacted evidence view to an unauthenticated viewer
Opening a valid share link SHALL render the same evidence document a logged-in
member sees for that run — request/response summaries, assertion detail,
screenshots — exactly as already redacted by the customer's CLI before upload.
The shared view SHALL NOT expose: other runs, the dashboard, project or
organization settings, tokens, member lists, trend or rollup analytics, or any
navigation to them. No login SHALL be required.

#### Scenario: Viewer sees the evidence and nothing else
- **WHEN** an unauthenticated person opens a valid share link
- **THEN** they see the run's redacted evidence document and have no link or route to any other run or to any account, project, or organization surface

#### Scenario: Run with no uploaded evidence
- **WHEN** a share link is opened for a run whose organization never opted into evidence upload
- **THEN** the view shows only the run's metadata-level result (result, classification, hashes) — the same non-evidence data the ingest contract always carries — and states no evidence document was uploaded

### Requirement: Share links are revocable and expire
Each share link SHALL have an expiry, SHALL be revocable by an admin at any time
before expiry, and SHALL stop working immediately on revocation or expiry. All
share links for a run SHALL be listable by an admin with their state.

#### Scenario: Revoked link stops working
- **WHEN** an admin revokes a share link and it is subsequently opened
- **THEN** access is denied and the page states the link is no longer valid

#### Scenario: Expired link stops working
- **WHEN** a share link is opened after its expiry
- **THEN** access is denied

#### Scenario: Deleting the run invalidates its links
- **WHEN** a run is purged by the evidence retention process
- **THEN** every share link for that run stops resolving

### Requirement: Sharing is a Team-gated entitlement
Creating share links SHALL be available only to organizations whose entitlements
include evidence sharing (Team and above). If an organization loses the
entitlement, existing links SHALL stop working until the entitlement is restored.

#### Scenario: Free organization cannot create a link
- **WHEN** an admin of a Free-tier organization attempts to create a share link
- **THEN** the request is rejected via the entitlement service with a reason naming the required tier

#### Scenario: Downgrade disables existing links
- **WHEN** an organization with active share links moves to a tier without the entitlement
- **THEN** those links return access-denied until the entitlement is restored, without being deleted

