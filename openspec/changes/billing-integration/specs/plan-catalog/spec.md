## MODIFIED Requirements

### Requirement: A single plan catalog defines every tier and its entitlements
The system SHALL define its commercial plans in one declarative catalog containing, for
each tier, a stable id, a display name, price metadata, a support description, and a
complete set of entitlement values. The catalog SHALL be the only source of tier and
entitlement data — no tier limit is compared or hardcoded anywhere else in the system.

Price metadata SHALL be a list of one or more cadence entries, each with a billing
interval, an amount, a unit, and whether the amount is a placeholder. The billing interval
SHALL be drawn from a fixed, validated vocabulary (at least `monthly` and `annual`); a
cadence entry with an unrecognised interval SHALL fail catalog validation. A tier MAY offer
a single cadence or several.

The entitlement set for a tier SHALL include at minimum: a maximum project count
(a number, or unlimited), whether the hosted evidence viewer is available, a maximum
evidence retention window in days (a number, or custom/unbounded), whether custom
redaction rules are available, whether project secret storage is available, whether trend
analytics is available, whether the release-readiness rollup is available, whether CI/PR
integration is available, whether SSO is available, and whether the audit log is
available.

#### Scenario: The catalog is loaded and validated at startup
- **WHEN** the API starts
- **THEN** it loads the catalog from its embedded definition and validates that every
  tier has a complete entitlement set and at least one cadence entry with a recognised
  interval
- **AND** a malformed or incomplete catalog fails startup rather than yielding an empty or
  partial entitlement set

#### Scenario: Three tiers are defined
- **WHEN** the catalog is read
- **THEN** it contains exactly the tiers `free`, `team`, and `enterprise`, in that order

#### Scenario: A tier offers monthly and annual pricing
- **WHEN** the catalog is read for the `team` tier
- **THEN** its price metadata contains a `monthly` cadence and an `annual` cadence, each
  with its own amount

#### Scenario: An unknown billing interval fails validation
- **WHEN** the catalog contains a cadence entry whose interval is not in the recognised
  vocabulary
- **THEN** catalog validation fails and the API does not start
