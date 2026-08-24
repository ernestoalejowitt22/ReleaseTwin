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
