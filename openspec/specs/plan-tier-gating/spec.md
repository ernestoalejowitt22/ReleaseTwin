# plan-tier-gating Specification

## Purpose

Gives every organization a plan tier (Free or Paid), enforces the first real entitlement limit on it (a project-count cap on Free), and provides a self-serve, no-payment way to lift that limit — a placeholder for the eventual paid flow, not billing itself.

## Requirements

### Requirement: Every organization has a plan tier, defaulting to Free
Every organization SHALL have a plan tier of either Free or Paid, set to Free at creation unless explicitly upgraded.

#### Scenario: New organizations start on Free
- **WHEN** a new organization is created (e.g. via first signup)
- **THEN** its plan tier is Free

### Requirement: Free-tier organizations are limited to one project
An organization on the Free tier SHALL be limited to one project; attempting to create a second project while on Free SHALL be rejected with a clear reason, not a silent failure or a generic error.

#### Scenario: A Free-tier organization's first project succeeds
- **WHEN** a Free-tier organization with no existing projects creates a project
- **THEN** the project is created successfully

#### Scenario: A Free-tier organization's second project is rejected
- **WHEN** a Free-tier organization that already has one project attempts to create a second
- **THEN** the request is rejected, and the response clearly states the reason is the Free-tier project limit

### Requirement: Paid-tier organizations have no project limit
An organization on the Paid tier SHALL NOT be subject to any project-count limit.

#### Scenario: A Paid-tier organization creates additional projects
- **WHEN** a Paid-tier organization that already has one or more projects creates another
- **THEN** the project is created successfully, regardless of how many projects already exist

### Requirement: An organization can self-serve upgrade to Paid without payment
A customer SHALL be able to move their own organization from Free to Paid through self-service, with no payment collected — an explicit placeholder for the eventual real paid flow.

#### Scenario: Upgrading lifts the project limit immediately
- **WHEN** a Free-tier organization at its one-project limit upgrades to Paid
- **THEN** it can immediately create additional projects, without any other action required

#### Scenario: Upgrading requires no payment information
- **WHEN** a customer upgrades their organization
- **THEN** no payment method, card, or billing information is requested or collected
