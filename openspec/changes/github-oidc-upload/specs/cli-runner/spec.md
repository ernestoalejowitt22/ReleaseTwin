## MODIFIED Requirements

### Requirement: Results are optionally uploaded to the hosted platform
If an upload credential resolves, the CLI SHALL upload each executed case's report to the ingest API after execution, mapped into the ingest contract. For a case run in flag-proof mode, the CLI SHALL upload its flag-proof result instead of a plain case report. A credential resolves in this order: a stored API token supplied via `RELEASETWIN_API_TOKEN`; else, when a project is named (`RELEASETWIN_PROJECT_ID`, or `project:` in the cases directory's `releasetwin.yml`, the environment winning when both are set) and the job carries GitHub Actions' OIDC request pair, a GitHub OIDC token requested for the hosted API's audience and exchanged at the platform's `POST /api/cli/auth/github` for a short-lived credential; else none. If no credential resolves, the CLI SHALL run and report exactly as before, with no upload attempted and no error raised. When a project is named and the OIDC exchange cannot complete, the CLI SHALL report the reason and the fix on one line, SHALL exit non-zero without executing cases, and SHALL still write the run summary (when one was requested) with `upload.mode = "oidc-exchange-failed"` and the reason. The exchanged credential SHALL be held in memory only and SHALL never be written to output or the summary. The run summary SHALL record how the upload authenticated in `upload.mode` (`token`, `oidc`, or `none`).

#### Scenario: Upload occurs when a token is configured
- **WHEN** the CLI runs with an API token supplied via environment variable
- **THEN** each executed case's report is uploaded to the ingest API after execution completes

#### Scenario: Flag-proof result is uploaded for flag-proof cases
- **WHEN** the CLI runs a case in flag-proof mode with an API token configured
- **THEN** the uploaded data is that case's flag-proof result, not a plain case report

#### Scenario: No upload is attempted without a token
- **WHEN** the CLI runs with no API token configured and no project named
- **THEN** it executes and reports cases exactly as it did before this capability existed, without attempting any upload and without treating the missing token as an error

#### Scenario: A stored token wins over OIDC
- **WHEN** both `RELEASETWIN_API_TOKEN` and a named project with GitHub's OIDC request pair are present
- **THEN** the stored token is used and no GitHub token is requested

#### Scenario: A named project on a GitHub Actions job uploads with an exchanged credential
- **WHEN** no stored token is set, `RELEASETWIN_PROJECT_ID` names a project, and `ACTIONS_ID_TOKEN_REQUEST_URL` and `ACTIONS_ID_TOKEN_REQUEST_TOKEN` are present
- **THEN** the CLI requests a GitHub OIDC token for audience `api.releasetwin.com`, exchanges it at the platform, uploads with the returned credential, and the summary records `upload.mode = "oidc"`

#### Scenario: The manifest can name the project
- **WHEN** `releasetwin.yml` in the cases directory carries `project: <id>` and the environment does not set `RELEASETWIN_PROJECT_ID`
- **THEN** that id is the named project; when both are set the environment's value is used

#### Scenario: A named project that cannot be exchanged stops the run loudly
- **WHEN** a project is named but the job has no OIDC request pair, or the platform refuses the exchange
- **THEN** the CLI prints one line naming the fix (for example `permissions: id-token: write`, or binding the repository on the project's Settings page), exits non-zero, executes no cases, and writes the requested summary with `upload.mode = "oidc-exchange-failed"` and the reason

#### Scenario: The credential is never disclosed
- **WHEN** a run uploads with an exchanged credential
- **THEN** the credential value appears in no output line and nowhere in the summary

### Requirement: The CLI can upload an existing JUnit report

The CLI SHALL provide a verb that uploads an existing JUnit XML file to the hosted platform's JUnit
ingest endpoint, so a suite that already emits JUnit can reach the dashboard without a case file
being authored. The verb SHALL take the path to the file and MAY take an optional release label,
which the CLI SHALL pass through to the platform.

The file SHALL be uploaded as received. The CLI SHALL NOT parse, rewrite, validate, or reformat its
contents, so the platform remains the single place JUnit dialect differences are interpreted.

The verb SHALL resolve the platform URL and credential the same way a run does, including the
GitHub OIDC exchange when a project is named. No resolvable credential SHALL be a clear error, not
a silent no-op — unlike a run, where uploading is an optional side effect, here the upload is the
entire purpose of the command. A failed OIDC exchange SHALL be reported with its reason and exit
non-zero.

#### Scenario: An existing report is uploaded

- **WHEN** the upload verb is invoked with a path to a JUnit XML file and an API token is configured
- **THEN** the file's bytes are sent to the platform's JUnit ingest endpoint unmodified

#### Scenario: A release label is passed through

- **WHEN** the upload verb is invoked with a release label
- **THEN** the platform receives that label alongside the uploaded report

#### Scenario: The contents are not interpreted locally

- **WHEN** the uploaded file uses a JUnit dialect the CLI has never seen
- **THEN** the CLI uploads it unchanged rather than rejecting it, leaving acceptance to the platform

#### Scenario: A missing file is an error

- **WHEN** the upload verb is invoked with a path that does not exist
- **THEN** the CLI reports the path it could not read and exits non-zero, making no network call

#### Scenario: A missing token is an error, not a silent success

- **WHEN** the upload verb is invoked with no API token configured and no project named
- **THEN** the CLI reports that a token or a named project on a GitHub Actions job is required and exits non-zero, uploading nothing

#### Scenario: The upload verb exchanges a GitHub token when a project is named

- **WHEN** the upload verb runs on a GitHub Actions job with `RELEASETWIN_PROJECT_ID` set and the OIDC request pair present, and no stored token
- **THEN** it uploads with the exchanged credential; and when the exchange fails it reports the reason and exits non-zero
