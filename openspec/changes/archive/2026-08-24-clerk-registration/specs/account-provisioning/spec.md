## MODIFIED Requirements

### Requirement: Signup requires no human approval
A prospective customer SHALL be able to create an account via a managed auth provider offering at least one sign-in method that does not presuppose an account on a specific unrelated third-party platform, and have it immediately usable, without any manual review, approval, or human interaction from the operator.

#### Scenario: New signup is immediately usable
- **WHEN** a prospective customer completes signup
- **THEN** they can create an organization and project immediately, without waiting for manual approval

#### Scenario: Signup does not require an unrelated platform account
- **WHEN** a prospective customer signs up
- **THEN** at least one available sign-in method does not require them to already hold an account on a platform unrelated to ReleaseTwin's own adapters (e.g. it does not require a GitHub account as the only path in)
