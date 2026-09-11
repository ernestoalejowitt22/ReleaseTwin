# local-evidence-viewer Specification

## Purpose
Lets anyone read a run's evidence in a browser straight off their own disk — no account, no
hosted platform, and no network call — so the free CLI's own artifact is legible, and shows
the same evidence document the hosted dashboard renders.
## Requirements
### Requirement: A local evidence directory can be rendered in a browser

The CLI SHALL provide a `view` subcommand that takes a local evidence directory — the layout
the CLI itself writes, one subdirectory per case id containing that case's evidence document
and its redacted screenshot files — serves a rendered report of it over HTTP on the local
machine, and prints the URL to open.

The subcommand SHALL make no network request of any kind: no upload, no hosted API call, no
telemetry, and no version or update check. It SHALL require no account, no API token, and no
configuration file, and SHALL read the directory without modifying it.

When no directory argument is given, the CLI SHALL use the same default it documents for the
evidence directory environment variable, and when the given path does not exist or contains
no case directories, the CLI SHALL say so and exit non-zero rather than serving an empty
report.

#### Scenario: A directory of cases is served

- **WHEN** a user runs the view subcommand against a directory containing one or more case
  subdirectories with evidence documents
- **THEN** the CLI serves a rendered report of those cases over HTTP on the local machine and
  prints the URL to open

#### Scenario: Viewing requires no account and no network

- **WHEN** the view subcommand runs on a machine with no network access, no API token, and no
  hosted configuration
- **THEN** the report is served and rendered in full, and no network request is attempted

#### Scenario: A missing or empty directory is an error, not an empty report

- **WHEN** the view subcommand is pointed at a path that does not exist, or at a directory
  containing no case subdirectories
- **THEN** the CLI reports what it looked for and exits non-zero without serving

#### Scenario: The evidence directory is not modified

- **WHEN** the view subcommand has served a directory and exited
- **THEN** the directory's contents are byte-for-byte unchanged

### Requirement: The rendered report shows the same evidence document the dashboard shows

For each case, the report SHALL render the evidence document's case identifier, oracle
locator, and redaction note, and for each leg its ordered steps with index, operation name,
outcome, duration, the assertion's expression / expected / observed where the step has one,
adapter-emitted evidence where present, and the case's redacted screenshots inline.

A leg carrying a name SHALL be presented as a distinct named section; a document with a
single unnamed leg SHALL be presented as one unnamed sequence of steps. Optional fields the
CLI omits when empty SHALL be treated as absent rather than required: a document in which any
optional field is missing entirely SHALL render without error.

The case list SHALL order cases so that cases containing a failed step appear before those
that do not, so the reason a run failed is the first thing visible.

The report SHALL state that the evidence was redacted by the CLI that produced it, and SHALL
label screenshots as best-effort-redacted, matching the document's own redaction note.

#### Scenario: A case's steps are rendered in pipeline order

- **WHEN** a case's evidence document is rendered
- **THEN** its steps appear in index order with their operation name, outcome, and duration,
  and each assertion step shows its expression, expected value, and observed value

#### Scenario: A flag-proof document's legs are distinct sections

- **WHEN** a rendered document carries named legs
- **THEN** each named leg is presented as its own section, labelled with that name

#### Scenario: An ordinary document's single leg is not labelled as a leg

- **WHEN** a rendered document carries one leg with no name
- **THEN** its steps are presented as a single unnamed sequence

#### Scenario: A document omitting optional fields renders

- **WHEN** a rendered step omits its assertion, adapter evidence, or screenshot references
  entirely
- **THEN** that step renders with its index, operation name, outcome, and duration, and the
  absent parts are shown as having no value rather than raising an error

#### Scenario: Failed cases are listed first

- **WHEN** the report lists a directory containing both cases with a failed step and cases
  without one
- **THEN** the cases containing a failed step are listed before the others

#### Scenario: Redaction is stated in the report

- **WHEN** a case's report is displayed
- **THEN** it states that redaction was performed by the CLI that produced the evidence, and
  labels any screenshot as best-effort-redacted

### Requirement: The rendered document shape is pinned against the hosted renderer

The evidence document shape the viewer renders SHALL be pinned by an automated test that
renders the viewer against a fixture held in this repository, in exactly the on-disk form the
CLI writes — optional fields omitted rather than null-valued.

That fixture SHALL be byte-identical to its counterpart in the hosted platform repository,
which that repository's own test renders its dashboard drill-down against. The two copies
existing and matching is what makes the documented claim — that the local viewer and the
hosted dashboard show the same evidence document — checkable rather than asserted.

The fixture SHALL contain no credential value, no token, and no unredacted response content.

#### Scenario: The viewer is tested against the on-disk fixture

- **WHEN** the test suite runs
- **THEN** the viewer is rendered against the repository's evidence fixture and its case
  identifier, oracle locator, redaction note, and every step's rendered values are asserted

#### Scenario: A shape change fails a test

- **WHEN** the evidence-document shape the viewer expects stops matching the fixture's on-disk
  shape
- **THEN** the pinning test fails

#### Scenario: The fixture carries no sensitive content

- **WHEN** the evidence fixture is read
- **THEN** it contains no credential value, no token, and no unredacted response body

### Requirement: Session video is shown when the run recorded one

When a session recording produced by the UI video directory is present for a case, the
rendered report SHALL play it inline alongside that case's evidence.

Video SHALL be additive and never required: a case with no recording SHALL render exactly as
it does when video recording was never enabled, and an unreadable or unplayable recording
SHALL NOT prevent the rest of that case's evidence from rendering.

Because session video is not part of the uploaded evidence document, it is the one thing the
local report shows that the hosted dashboard does not. Nothing in this capability SHALL
describe the hosted view as showing video.

#### Scenario: A recorded session plays in the report

- **WHEN** a case has a session recording in the configured video directory
- **THEN** the report plays that recording inline with that case's evidence

#### Scenario: A case with no recording renders unchanged

- **WHEN** a case has no session recording
- **THEN** its evidence renders in full with no video area and no error

### Requirement: A run's evidence can be exported as one self-contained file

The CLI SHALL provide an export mode on the view subcommand that writes the same rendered
report to a single file instead of serving it, with the evidence and images embedded in that
file so it renders correctly with no other file, no server, and no network access.

The exported file SHALL be openable directly from a filesystem, so it can be attached to a
pull request, a ticket, or an email. Export SHALL make no network request, and SHALL report
where it wrote the file.

#### Scenario: The export renders on its own

- **WHEN** an exported file is opened from a filesystem with no server running and no network
  access, and with the original evidence directory deleted
- **THEN** the report renders in full, including its screenshots

#### Scenario: Export reports its destination

- **WHEN** the export completes
- **THEN** the CLI prints the path it wrote

### Requirement: Viewing works from the published container image

Because the CLI is distributed as a container image, the view subcommand SHALL serve on an
address reachable from outside its container when the container's port is published, and
SHALL always print the URL to open rather than depending on a browser being present.

The CLI MAY open the user's browser directly when it is not running containerized, but SHALL
NOT treat a failure to open a browser as a failure of the command.

#### Scenario: The served report is reachable from outside the container

- **WHEN** the view subcommand runs inside the published container image with its port
  published to the host
- **THEN** the report is reachable from a browser on the host at the printed URL

#### Scenario: No browser is not an error

- **WHEN** the view subcommand runs in an environment with no browser available
- **THEN** it serves the report, prints the URL, and does not exit non-zero for that reason

