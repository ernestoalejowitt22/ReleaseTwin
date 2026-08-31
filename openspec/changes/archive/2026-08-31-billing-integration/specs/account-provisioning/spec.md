## ADDED Requirements

### Requirement: An organization records its Merchant-of-Record billing linkage
An organization SHALL carry the identifiers linking it to its Merchant-of-Record customer
and subscription, its billing status, and its billing cadence, alongside its tier. These
SHALL default to an unlinked, active state at creation and for any organization that has
never checked out, so that a newly signed-up or operator-set organization is fully usable
with no billing linkage. Reading an organization stored before these fields existed SHALL
succeed and yield the unlinked, active defaults.

#### Scenario: A new organization has no billing linkage
- **WHEN** an organization is created at first signup
- **THEN** it has no Merchant-of-Record customer or subscription identifier, its billing
  status is active, and it is fully usable on the Free tier

#### Scenario: An operator-set organization needs no billing linkage
- **WHEN** an operator sets an organization to `Enterprise`
- **THEN** the organization functions with its Enterprise entitlements and no
  Merchant-of-Record subscription

#### Scenario: An organization stored before billing fields existed still loads
- **WHEN** an organization row that predates the billing fields is read
- **THEN** it loads successfully with an unlinked customer/subscription and active billing
  status
