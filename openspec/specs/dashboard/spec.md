# dashboard Specification

## Purpose

The hosted web UI a signed-up customer uses to see their own uploaded run history and flag-proof results — the payoff for having self-served through signup, token issuance, and a CLI upload, without ever talking to a person.

## Requirements

### Requirement: Dashboard access requires an authenticated web session
Viewing dashboard data SHALL require an authenticated web session (e.g. via OAuth or a magic-link login), separate from and never satisfied by an API token.

#### Scenario: Unauthenticated access is denied
- **WHEN** the dashboard is accessed without a valid web session
- **THEN** access is denied and the visitor is directed to sign in

#### Scenario: An API token alone does not grant dashboard access
- **WHEN** a request presents only an API token, not a web session
- **THEN** the dashboard denies access

### Requirement: A customer sees only their own organization's data
The dashboard SHALL display only reports uploaded to projects within the signed-in customer's own organization.

#### Scenario: Cross-organization data is never shown
- **WHEN** a customer views the dashboard
- **THEN** no report belonging to a different organization appears, under any view or filter

### Requirement: Run history is visible
The dashboard SHALL display the history of uploaded case reports for a project, including pass/fail outcome and failure classification per case.

#### Scenario: Uploaded reports appear in run history
- **WHEN** a case report has been uploaded for a project
- **THEN** it appears in that project's run history with its outcome and classification visible

### Requirement: Flag-proof outcomes are shown distinctly from ordinary case results
The dashboard SHALL present flag-proof (paired known-bad/known-good) results as a distinct kind of result, showing the outcome (passed / weak oracle / both failed / inconclusive) rather than folding it into an ordinary pass/fail case entry.

#### Scenario: Flag-proof result is not shown as an ordinary pass/fail
- **WHEN** an uploaded flag-proof result is displayed
- **THEN** its outcome (e.g. weak oracle) is visible as such, distinguishable from an ordinary case's pass/fail

### Requirement: Token issuance surfaces install/run instructions
Immediately alongside a newly issued API token, the dashboard SHALL display a runnable command that sets the token as an environment variable and a CLI invocation that runs a zero-credential example case, so the customer has something to copy-paste and execute without leaving the page.

#### Scenario: Instructions appear with the token
- **WHEN** a customer issues a new API token
- **THEN** the display includes both the token value and a copy-paste command sequence that sets it as an environment variable and runs the CLI against a zero-credential example case

### Requirement: Token usage is presented as optional
Alongside the token instructions, the dashboard SHALL state that setting the token is optional: cases run without it stay fully local, and setting it is what links a run to this project.

#### Scenario: Optionality is explained
- **WHEN** token issuance instructions are displayed
- **THEN** the text states that running the CLI without the token stays local and free, and that setting the token is what links future runs to this project

### Requirement: Dashboard shows the organization's current usage
The dashboard SHALL display the signed-in customer's organization-wide uploaded report count for the current period, independent of which single project is currently selected.

#### Scenario: Usage summary is visible regardless of selected project
- **WHEN** a customer with multiple projects in their organization views the dashboard with one project selected
- **THEN** the displayed usage count reflects all of the organization's projects combined, not only the selected one

#### Scenario: Usage summary reflects zero usage honestly
- **WHEN** an organization has no uploaded reports in the current period
- **THEN** the dashboard displays a usage count of zero rather than omitting the summary

### Requirement: Dashboard shows the organization's plan tier and an upgrade path
The dashboard SHALL display the signed-in customer's organization's current plan tier, and when on Free, SHALL offer a self-serve control to upgrade to Paid.

#### Scenario: Free-tier customer sees an Upgrade control
- **WHEN** a customer whose organization is on the Free tier views the dashboard
- **THEN** the current tier is shown, alongside a control to upgrade

#### Scenario: Paid-tier customer does not see an Upgrade control
- **WHEN** a customer whose organization is on the Paid tier views the dashboard
- **THEN** the current tier is shown, with no upgrade control offered
