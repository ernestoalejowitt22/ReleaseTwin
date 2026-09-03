# ci-report-formats Specification

## Purpose

Defines the CLI's obligation to emit a portable, platform-neutral test report
(JUnit XML) for a run — case results and the flag-proof verdict mapping — written
only on request and carrying no bodies or secrets, so any CI platform's native
test-report ingestion can render a ReleaseTwin run without ReleaseTwin-specific
code.

## Requirements

### Requirement: The CLI emits a JUnit XML report on request

The CLI SHALL write a JUnit-XML test report describing the run when, and only
when, the caller asks for one — via a `--junit-xml <path>` argument or a
`RELEASETWIN_JUNIT_XML` environment variable, with the argument winning when both
are present. The report SHALL be written after the run finishes, on pass or fail,
alongside the CLI's normal human-readable output and independently of any
`--summary-json` report.

When neither the argument nor the environment variable is set, the CLI SHALL NOT
write a JUnit file and its behavior SHALL be byte-for-byte what it is without this
capability.

#### Scenario: The flag requests a report

- **WHEN** the CLI runs a directory of cases with `--junit-xml report.xml`
- **THEN** after the run a file `report.xml` exists containing a JUnit XML
  document describing the run

#### Scenario: The environment variable requests a report

- **WHEN** the CLI runs with `RELEASETWIN_JUNIT_XML` set to a path and no
  `--junit-xml` argument
- **THEN** the report is written to that path

#### Scenario: The argument wins over the environment variable

- **WHEN** the CLI runs with both `--junit-xml a.xml` and `RELEASETWIN_JUNIT_XML=b.xml`
- **THEN** the report is written to `a.xml` and not to `b.xml`

#### Scenario: No flag, no file

- **WHEN** the CLI runs with neither the argument nor the environment variable
- **THEN** no JUnit file is written and the CLI's output is unchanged

#### Scenario: The destination directory does not exist

- **WHEN** the CLI is given `--junit-xml` a path whose parent directory does not exist
- **THEN** the CLI reports a one-line error naming the missing directory and does
  not start the run

### Requirement: The JUnit report maps every case outcome and flag-proof verdict

The report SHALL contain exactly one `<testcase>` per case that ran, each
carrying the case id as its `name` and a stable `classname` identifying the run.
A `<testsuite>` (or `<testsuites>`) element SHALL carry the total case count and
the failure count consistent with the individual `<testcase>` entries.

Each `<testcase>` SHALL represent its outcome as follows:

- a case that passed and is not a flag-proof case, or a flag-proof case whose
  outcome is `Passed` — no child element (a JUnit pass);
- every other outcome — a `<failure>` child whose `message` attribute names the
  ReleaseTwin outcome. Specifically: a plain case that failed (`message` = its
  failure classification when one exists, else `failed`); and a flag-proof case
  whose outcome is `WeakOracle`, `BothFailed`, `Inverted`, `Ineligible`,
  `ControlFailed`, or `ControlUnverified` (`message` = the `FlagProofOutcome`
  name). A flag-proof case that requested a paired run and did not get one
  (`Ineligible` / `ControlFailed` / `ControlUnverified`) is a failure in the
  report, not a skip — the widget treats "flag proof requested but not
  performed" as red.

The report SHALL NOT emit `<skipped>` for any outcome. The mapping SHALL be
total: every `FlagProofOutcome` value and every plain-case outcome SHALL resolve
to exactly one of pass or `<failure>`.

#### Scenario: A passing plain case

- **WHEN** a non-flag-proof case passes
- **THEN** its `<testcase>` has no `<failure>` and no `<skipped>` child

#### Scenario: A proven flag-proof case

- **WHEN** a flag-proof case has outcome `Passed`
- **THEN** its `<testcase>` has no `<failure>` and no `<skipped>` child

#### Scenario: A weak oracle is a failure that names the verdict

- **WHEN** a flag-proof case has outcome `WeakOracle`
- **THEN** its `<testcase>` has a `<failure>` child whose `message` contains `WeakOracle`

#### Scenario: An inverted oracle is a failure that names the verdict

- **WHEN** a flag-proof case has outcome `Inverted`
- **THEN** its `<testcase>` has a `<failure>` child whose `message` contains `Inverted`

#### Scenario: An ineligible flag-proof case is a failure

- **WHEN** a flag-proof case has outcome `Ineligible`
- **THEN** its `<testcase>` has a `<failure>` child whose `message` contains `Ineligible`

#### Scenario: A control-unverified flag-proof case is a failure

- **WHEN** a flag-proof case has outcome `ControlUnverified`
- **THEN** its `<testcase>` has a `<failure>` child whose `message` contains `ControlUnverified`

#### Scenario: The report never emits skipped

- **WHEN** a run produces any mix of case outcomes, including `Ineligible`,
  `ControlFailed`, and `ControlUnverified`
- **THEN** the report contains no `<skipped>` element

#### Scenario: Suite counts match the cases

- **WHEN** a run of N cases produces F `<failure>` cases
- **THEN** the `<testsuite>` reports `tests="N"` and `failures="F"`

### Requirement: The JUnit report carries no bodies or secrets

The JUnit report SHALL contain only metadata the CLI already prints — case ids,
outcomes, failure classifications, flag-proof outcome names, and timing. It SHALL
NOT contain fixture content, request or response bodies, header values, or any
credential value, whether or not evidence capture was enabled for the run.

#### Scenario: A run with evidence enabled still emits a body-free report

- **WHEN** the CLI runs with evidence capture enabled and `--junit-xml`
- **THEN** the report contains no request/response body text and no header or
  credential values

### Requirement: The report is well-formed JUnit XML consumable by CI platforms

The report SHALL be a well-formed XML document using the widely-supported JUnit
element vocabulary (`testsuites`/`testsuite`/`testcase`/`failure`) such that a
standard CI JUnit ingester (GitLab `artifacts:reports:junit`, Azure Pipelines
`PublishTestResults`, CircleCI `store_test_results`, the Jenkins `junit` step)
parses it without error and shows one result row per case.

#### Scenario: The report parses as XML

- **WHEN** the report file is loaded by an XML parser
- **THEN** it parses without error and its root is `testsuites` or `testsuite`

#### Scenario: Text is XML-escaped

- **WHEN** a case id or a failure message contains a character that is special in
  XML (`<`, `&`, `"`)
- **THEN** that character is escaped in the report and the document still parses
