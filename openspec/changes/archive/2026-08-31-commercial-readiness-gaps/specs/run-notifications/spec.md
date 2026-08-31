## Purpose

Pushes a signal to the customer's own channel when a run they care about fails,
so release-proof evidence is acted on instead of sitting unseen on a dashboard.

## ADDED Requirements

### Requirement: A project can have outbound notification targets
An admin SHALL be able to configure, per project, zero or more outbound
notification targets. Each target SHALL be one of: a Slack incoming webhook URL,
or a generic HTTPS webhook URL. Targets SHALL be individually enabled, disabled,
and deleted. A target's URL SHALL be validated on save to be an HTTPS URL and
SHALL be rejected if it resolves to a private, loopback, or link-local address.

#### Scenario: Admin adds a Slack target
- **WHEN** an admin adds a Slack incoming webhook URL as a notification target for a project
- **THEN** the target is stored, shown as enabled, and used for subsequent qualifying events

#### Scenario: Non-HTTPS or private-address URL is refused
- **WHEN** an admin submits a notification target URL that is not HTTPS, or that resolves to a private/loopback/link-local address
- **THEN** the target is not saved and the response states why

#### Scenario: Disabled target is skipped
- **WHEN** a qualifying event occurs and a project has a disabled notification target
- **THEN** no delivery is attempted to that target

### Requirement: Failure events trigger delivery
The system SHALL attempt delivery to every enabled target of a project when a run
ingested for that project has an overall result of failed, or contains a
flag-proof result that is failed or ineligible. Successful runs and runs with no
qualifying condition SHALL NOT trigger delivery.

#### Scenario: Failed run notifies
- **WHEN** a run is ingested for a project with an enabled target and the run's overall result is failed
- **THEN** a notification is delivered to that target

#### Scenario: Failed flag proof notifies
- **WHEN** a run is ingested whose flag-proof result is failed or ineligible
- **THEN** a notification is delivered even if the rest of the run passed

#### Scenario: Passing run is silent
- **WHEN** a run is ingested that passed and whose flag proof (if any) passed
- **THEN** no notification is delivered

### Requirement: Notification payload carries a link back and no sensitive content
A delivered notification SHALL identify the project, the case or run identifier,
the result and classification, and SHALL include a link to the run on the hosted
dashboard. It SHALL NOT include fixture content, response bodies, credentials, or
any evidence-document detail.

#### Scenario: Payload contents
- **WHEN** a notification is delivered for a failed run
- **THEN** it contains the project name, the run/case identifier, the result and failure classification, and a dashboard link, and contains none of: fixture content, response bodies, secrets

### Requirement: Delivery is retried and failures are visible
Delivery SHALL be attempted asynchronously and SHALL NOT block or fail run
ingestion. A failed delivery SHALL be retried a bounded number of times with
backoff. The most recent delivery outcome per target SHALL be visible to an
admin.

#### Scenario: Ingest is unaffected by a broken target
- **WHEN** a notification target's endpoint is unreachable or returns an error
- **THEN** run ingestion still succeeds and returns normally

#### Scenario: Admin sees last delivery status
- **WHEN** an admin views a project's notification targets after a delivery attempt
- **THEN** each target shows whether its last attempt succeeded or failed and when

### Requirement: Notifications are a Team-gated entitlement
Configuring and delivering run notifications SHALL be available only to
organizations whose entitlements include run notifications (Team and above).
A Free organization SHALL be prevented from adding targets with a message naming
the required tier, and SHALL receive no deliveries.

#### Scenario: Free organization cannot configure targets
- **WHEN** an admin of a Free-tier organization attempts to add a notification target
- **THEN** the request is rejected via the entitlement service with a reason naming the required tier

#### Scenario: Downgrade stops delivery
- **WHEN** an organization with configured targets moves to a tier without the entitlement
- **THEN** no further notifications are delivered while the entitlement is absent
