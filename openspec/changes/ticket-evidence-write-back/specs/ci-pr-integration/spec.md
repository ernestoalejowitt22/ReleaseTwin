## MODIFIED Requirements

### Requirement: The run summary MAY carry dashboard URLs and the Action renders them

The machine-readable run summary MAY carry an optional run-level dashboard URL,
optional per-case evidence URLs, and each case's oracle locator (verbatim from
the case file's `oracle.locator`, omitted when the case declared none),
populated by the CLI. Dashboard and evidence URLs are populated only when a
report upload succeeded; the oracle locator is populated whenever the case
declared one, independent of any upload. The summary's `schemaVersion` SHALL
be incremented whenever a field is added; a consumer that ignores unknown
fields SHALL be unaffected, and a run that populates none of these optional
fields SHALL produce a summary whose only difference from the prior version is
the version integer.

When the summary carries a run-level URL, the Action SHALL render it as a link in
the pull-request comment and SHALL set the check run's details URL to it. When a
case entry carries an evidence URL, the Action SHALL render that case's row in the
comment as a link to it. When the summary carries none of these optional fields, the
comment and check run SHALL be byte-for-byte what they are today.

The Action SHALL still require only `GITHUB_TOKEN`; the URLs are read from the
summary file the CLI already produced, and their absence is the normal
no-ReleaseTwin-account path.

#### Scenario: A summary with a run URL produces a linked comment and check

- **WHEN** the Action renders a summary that carries a run-level dashboard URL
- **THEN** the PR comment contains a link to that URL and the check run's details URL is set to it

#### Scenario: A failing case with an evidence URL links from its row

- **WHEN** the summary's entry for a failed case carries an evidence URL
- **THEN** that case's row in the PR comment links to the evidence URL

#### Scenario: A summary with no URLs renders exactly as before

- **WHEN** the Action renders a summary produced by a run that performed no upload and whose cases declared no oracle locators
- **THEN** the PR comment and check run are identical to the output before this capability existed

#### Scenario: The Action still needs no ReleaseTwin credentials

- **WHEN** the Action's required inputs and permissions are inspected
- **THEN** it still requires only `GITHUB_TOKEN` and does not reference a ReleaseTwin API token or hosted endpoint

#### Scenario: A case's oracle locator is carried into the summary

- **WHEN** the CLI produces a summary for a case whose case file declares `oracle.locator`
- **THEN** that case's entry in the summary carries the same locator string, unchanged
