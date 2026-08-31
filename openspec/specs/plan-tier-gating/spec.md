# plan-tier-gating Specification

## Purpose

Gives every organization a plan tier (Free, Team, or Enterprise) and enforces its limits from the shared plan catalog via the entitlement service — never by comparing a tier value inline. Provides a self-serve, no-payment way to move from Free to Team, a placeholder for the eventual paid flow, not billing itself; Enterprise is operator-set.
## Requirements
### Requirement: Every organization has a plan tier, defaulting to Free
Every organization SHALL have a plan tier that is one of `Free`, `Team`, or `Enterprise`,
set to `Free` at creation unless explicitly upgraded. The tier is stored as a single
attribute on the organization; there is no plan history and no concurrent plans.

#### Scenario: New organizations start on Free
- **WHEN** a new organization is created (e.g. via first signup)
- **THEN** its plan tier is `Free`

#### Scenario: A previously "Paid" organization reads as Team
- **WHEN** an organization whose tier was stored under the earlier two-tier model as
  "Paid" is loaded
- **THEN** it resolves to `Team` and is rewritten to `Team` on its next save

### Requirement: Free-tier organizations are limited to one project
An organization's maximum project count SHALL be taken from its tier's `maxProjects`
entitlement in the plan catalog. On the `Free` tier this is one. Attempting to create a
project beyond the tier's limit SHALL be rejected with a clear reason naming the tier
limit, not a silent failure or a generic error. A tier whose `maxProjects` entitlement is
unlimited SHALL impose no project-count limit.

#### Scenario: A Free-tier organization's first project succeeds
- **WHEN** a Free-tier organization with no existing projects creates a project
- **THEN** the project is created successfully

#### Scenario: A Free-tier organization's second project is rejected
- **WHEN** a Free-tier organization that already has one project attempts to create a second
- **THEN** the request is rejected, and the response clearly states the reason is the
  Free-tier project limit

### Requirement: Paid-tier organizations have no project limit
An organization on a tier whose `maxProjects` entitlement is unlimited (`Team` and
`Enterprise` in the current catalog) SHALL NOT be subject to any project-count limit.

#### Scenario: A Paid-tier organization creates additional projects
- **WHEN** a `Team` organization that already has one or more projects creates another
- **THEN** the project is created successfully, regardless of how many projects already exist

### Requirement: Run notifications and evidence sharing are Team-gated entitlements
The plan catalog SHALL define two additional entitlement keys — one for run
notifications and one for evidence sharing — and the entitlement service SHALL
report them as granted for `Team` and `Enterprise` and denied for `Free`.
Enforcement of these features SHALL route through the entitlement service and
SHALL NOT compare the tier value inline.

#### Scenario: Free is denied both entitlements
- **WHEN** the entitlement service is asked whether a `Free` organization may configure run notifications or create evidence share links
- **THEN** both are reported as denied, each with a reason naming the required tier

#### Scenario: Team is granted both entitlements
- **WHEN** the entitlement service is asked the same for a `Team` organization
- **THEN** both are reported as granted

#### Scenario: Losing the tier revokes the entitlement
- **WHEN** an organization moves from `Team` to `Free`
- **THEN** the entitlement service immediately reports both entitlements as denied, and features that depend on them stop operating

### Requirement: An organization can self-serve upgrade to Paid
A customer SHALL be able to move their own organization from `Free` to `Team` through
self-service by paying through the Merchant of Record's hosted checkout (see the `billing`
capability). Payment details SHALL be collected only on the Merchant of Record's surface,
never by the hosted platform itself. The tier SHALL change to `Team` as a result of
processing a subscription notification, not as a direct action of the checkout or
redirect-return code. Moving an organization to `Enterprise` SHALL NOT be self-serve; it is
set by an operator.

#### Scenario: Upgrading lifts the project limit immediately
- **WHEN** a `Free`-tier organization at its one-project limit completes checkout and its
  subscription notification is processed
- **THEN** it is on `Team` and can immediately create additional projects, without any
  other action required

#### Scenario: Upgrading requires no payment information
- **WHEN** a customer upgrades their organization to `Team`
- **THEN** no card, payment-method, or billing-address field is presented by the hosted
  platform itself — all payment data is collected on the Merchant of Record's surface

#### Scenario: Enterprise is not reachable by self-service
- **WHEN** a customer attempts to set their own organization's tier to `Enterprise`
- **THEN** the request is rejected; the tier can only be set to `Enterprise` by an operator

### Requirement: Projects in excess of the current tier limit are read-only, not deleted
When an organization's project count exceeds its tier's `maxProjects` entitlement — reachable
only after a downgrade or cancellation, since project creation is blocked when it would
exceed the limit — the oldest projects up to the limit SHALL remain writable and the
remainder SHALL become read-only: still visible on the dashboard with their existing
evidence, but rejecting new evidence ingest with an `entitlement-required` error. No project
SHALL be deleted or hidden as a result of a downgrade. Re-upgrading or deleting projects to
get under the limit SHALL restore all remaining projects to writable.

#### Scenario: Downgrade makes the newest projects read-only
- **WHEN** a `Team` organization with three projects is downgraded to `Free` (limit one)
- **THEN** its oldest project stays writable, the other two become read-only, and all three
  remain visible with their evidence intact

#### Scenario: Read-only project rejects ingest
- **WHEN** an API token for a read-only project is used to upload a new case report
- **THEN** the request is rejected with an `entitlement-required` error

#### Scenario: Re-upgrading restores all projects
- **WHEN** a downgraded organization with read-only projects returns to a tier whose
  `maxProjects` covers its project count
- **THEN** all of its projects are writable again

### Requirement: Project creation and deletion keep the paid subscription quantity in sync
For an organization with an active paid subscription, creating a project SHALL raise the
Merchant-of-Record subscription quantity before the project is created and SHALL fail the
creation (creating no project) if the Merchant of Record rejects the increase. Deleting a
project SHALL lower the quantity but SHALL NOT be blocked by a failure to do so. An
organization with no Merchant-of-Record subscription (operator-set or hand-invoiced) SHALL
be unaffected by this requirement.

#### Scenario: Creating a project on a paid subscription bumps quantity first
- **WHEN** a customer with an active paid subscription creates a project
- **THEN** the subscription quantity is increased and, only on success, the project is created

#### Scenario: A billing rejection blocks project creation
- **WHEN** the Merchant of Record rejects the quantity increase during project creation
- **THEN** no project is created and the error names the payment problem and points to the
  customer portal

#### Scenario: Operator-set organizations are not billed for projects
- **WHEN** an `Enterprise` organization set by an operator, with no Merchant-of-Record
  subscription, creates or deletes a project
- **THEN** no billing call is made

