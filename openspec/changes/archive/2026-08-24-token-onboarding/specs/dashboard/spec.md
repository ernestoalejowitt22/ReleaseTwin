## ADDED Requirements

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
