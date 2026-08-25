## ADDED Requirements

### Requirement: Dashboard shows the organization's plan tier and an upgrade path
The dashboard SHALL display the signed-in customer's organization's current plan tier, and when on Free, SHALL offer a self-serve control to upgrade to Paid.

#### Scenario: Free-tier customer sees an Upgrade control
- **WHEN** a customer whose organization is on the Free tier views the dashboard
- **THEN** the current tier is shown, alongside a control to upgrade

#### Scenario: Paid-tier customer does not see an Upgrade control
- **WHEN** a customer whose organization is on the Paid tier views the dashboard
- **THEN** the current tier is shown, with no upgrade control offered
