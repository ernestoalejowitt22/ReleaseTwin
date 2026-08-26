## MODIFIED Requirements

### Requirement: The connected repo is chosen from a real list, not free text
The customer SHALL select the repository to connect from a list fetched live from GitHub during the
connection flow, not by typing an arbitrary string. The list SHALL include the customer's private
repositories, not only public ones.

#### Scenario: Picker reflects the customer's actual repositories
- **WHEN** a customer completes the GitHub authorization step
- **THEN** the list of repositories offered for selection is fetched live from GitHub for that
  customer's account

#### Scenario: A private repository appears in the picker
- **WHEN** a customer completes the GitHub authorization step and owns a private repository
- **THEN** that private repository appears in the list offered for selection, alongside any public
  ones

### Requirement: A connection is display metadata only
A project's connection SHALL NOT grant the hosted platform any ability to read, list, or act on data
from the connected repository beyond the repository identifier itself. This describes what this
app's own code does with a connection, not a technical ceiling enforced by the OAuth scope granted —
the scope requested during authorization MAY be broader than what this app exercises (e.g. broad
enough to also include private repository content), but no code path in this app SHALL use that
broader access for anything beyond fetching the repository list once, during the connection flow
itself.

#### Scenario: No repository content is ever fetched after connecting
- **WHEN** a project has an active connection
- **THEN** no code path reads pull requests, commits, files, or any other content from that
  repository — only its identifier is ever stored

#### Scenario: A broader OAuth grant is not exercised beyond listing repositories
- **WHEN** the OAuth scope requested during authorization is broad enough to permit reading
  repository content
- **THEN** this app still never reads anything from the connected repository beyond fetching the
  repository list during the connection flow, and never persists the access token used to do so
