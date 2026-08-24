## Purpose

Lets a signed-in customer label a project with the external GitHub repository it corresponds to, chosen via a real OAuth-driven picker rather than free text, without the hosted platform ever holding or acting on the customer's GitHub credentials beyond that single selection request.

## ADDED Requirements

### Requirement: Connecting a project requires an authenticated web session
Starting or completing a GitHub connection for a project SHALL require the same authenticated web session the dashboard itself requires, and SHALL only operate on projects belonging to the signed-in customer's own organization.

#### Scenario: Unauthenticated connection attempt is denied
- **WHEN** a connection flow is started without a valid web session
- **THEN** access is denied, consistent with any other dashboard action

#### Scenario: A project outside the signed-in organization cannot be connected
- **WHEN** a connection is attempted for a project belonging to a different organization
- **THEN** the request is rejected, regardless of what project ID is supplied

### Requirement: The connected repo is chosen from a real list, not free text
The customer SHALL select the repository to connect from a list fetched live from GitHub during the connection flow, not by typing an arbitrary string.

#### Scenario: Picker reflects the customer's actual repositories
- **WHEN** a customer completes the GitHub authorization step
- **THEN** the list of repositories offered for selection is fetched live from GitHub for that customer's account

### Requirement: The GitHub access token is never persisted
The access token obtained during a connection flow SHALL be used only to fetch the repository list for that single flow and SHALL NOT be written to the database, session store, cookie, or any log.

#### Scenario: No token appears anywhere after a connection completes
- **WHEN** a project's connection flow completes successfully
- **THEN** no record of the GitHub access token used exists in the database, session store, or application logs

### Requirement: A connected project displays its linked repository
The dashboard SHALL display which external repository (if any) a project is connected to.

#### Scenario: Connected project shows its repo
- **WHEN** a project has an active connection
- **THEN** the dashboard shows the connected repository's identifier alongside the project

### Requirement: A connection can be removed through self-service
A customer SHALL be able to remove a project's connection without any operator action, and once removed the project SHALL display as unconnected.

#### Scenario: Disconnecting removes the displayed link
- **WHEN** a customer disconnects a project's GitHub connection
- **THEN** the dashboard no longer shows that project as connected to any repository

### Requirement: A connection is display metadata only
A project's connection SHALL NOT grant the hosted platform any ability to read, list, or act on data from the connected repository beyond the repository identifier itself.

#### Scenario: No repository content is ever fetched after connecting
- **WHEN** a project has an active connection
- **THEN** no code path reads pull requests, commits, files, or any other content from that repository — only its identifier is ever stored
