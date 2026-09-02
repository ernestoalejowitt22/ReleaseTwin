## MODIFIED Requirements

### Requirement: A project can have outbound notification targets

An admin SHALL be able to configure, per project, zero or more outbound
notification targets. Each target SHALL be one of: a Slack incoming webhook URL,
or a generic HTTPS webhook URL. Targets SHALL be individually enabled, disabled,
and deleted. A target's URL SHALL be validated on save to be an HTTPS URL and
SHALL be rejected if it resolves to a private, loopback, or link-local address.

The same address check SHALL be re-applied immediately before each delivery, and
the delivery connection SHALL be made to the exact address that check approved —
not to an address obtained by a separate later name resolution — so that a
name whose resolution changes between the check and the connection cannot cause
delivery to a private, loopback, or link-local address.

#### Scenario: Admin adds a Slack target

- **WHEN** an admin adds a Slack incoming webhook URL as a notification target for a project
- **THEN** the target is stored, shown as enabled, and used for subsequent qualifying events

#### Scenario: Non-HTTPS or private-address URL is refused

- **WHEN** an admin submits a notification target URL that is not HTTPS, or that resolves to a private/loopback/link-local address
- **THEN** the target is not saved and the response states why

#### Scenario: Disabled target is skipped

- **WHEN** a qualifying event occurs and a project has a disabled notification target
- **THEN** no delivery is attempted to that target

#### Scenario: A name that re-resolves to a private address at send time is not delivered to

- **WHEN** a target host passes the private-address check but, at delivery time, a fresh resolution would return a private, loopback, or link-local address
- **THEN** no request is sent to that address and the delivery is recorded as failed with the reason
