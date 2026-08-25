## ADDED Requirements

### Requirement: Dashboard shows the organization's current usage
The dashboard SHALL display the signed-in customer's organization-wide uploaded report count for the current period, independent of which single project is currently selected.

#### Scenario: Usage summary is visible regardless of selected project
- **WHEN** a customer with multiple projects in their organization views the dashboard with one project selected
- **THEN** the displayed usage count reflects all of the organization's projects combined, not only the selected one

#### Scenario: Usage summary reflects zero usage honestly
- **WHEN** an organization has no uploaded reports in the current period
- **THEN** the dashboard displays a usage count of zero rather than omitting the summary
