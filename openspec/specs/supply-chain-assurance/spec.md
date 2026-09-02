# supply-chain-assurance Specification

## Purpose
Defines what the project's own CI pipeline must check before code merges or
deploys — secrets, dependency vulnerabilities, and static analysis — and the
integrity rules for tools the pipeline pulls in, so the build process behind a
test-evidence product is itself defensible under review.
## Requirements
### Requirement: Every change is scanned for committed secrets

The CI pipeline SHALL scan for committed secrets on every pull request and on
every push to the default branch, and SHALL run a full commit-history sweep on a
recurring schedule. A detected secret SHALL fail the check and block merge.

#### Scenario: A secret in a pull request blocks merge

- **WHEN** a pull request adds a file containing a credential-shaped value the scanner recognizes
- **THEN** the secret-scan check fails and the pull request cannot be merged until it is resolved

#### Scenario: History is swept on a schedule

- **WHEN** the scheduled sweep runs
- **THEN** it scans the full commit history, not only the current tree, and reports any finding

### Requirement: Dependencies are scanned for known vulnerabilities

The CI pipeline SHALL check the project's resolved dependency graph — including
transitive dependencies — for known security advisories on every pull request,
covering both the .NET solution and the web application. A pull request that
introduces a dependency (direct or transitive) with a high or critical severity
advisory SHALL fail the check. The project SHALL also run automated dependency-update
proposals so advisories in existing dependencies surface as their own change.

#### Scenario: A new high-severity advisory blocks the pull request

- **WHEN** a pull request adds or bumps a dependency that resolves to a package with a high or critical advisory
- **THEN** the dependency-scan check fails and names the offending package and advisory

#### Scenario: A pull request with no vulnerable dependencies passes

- **WHEN** a pull request's resolved dependency graph contains no high or critical advisories
- **THEN** the dependency-scan check passes

#### Scenario: Advisories in existing dependencies are raised automatically

- **WHEN** a new advisory is published against a dependency already in the project
- **THEN** an automated update proposal is opened for it without a maintainer having to notice manually

### Requirement: Static analysis runs on every change

The CI pipeline SHALL run static application security testing across the
project's compiled languages (C#) and its web sources (JavaScript/TypeScript) on
every pull request and on a recurring schedule. A newly introduced finding at or
above the configured severity threshold SHALL fail the pull-request check.

#### Scenario: A new SAST finding fails the pull request

- **WHEN** a pull request introduces code that the analyzer flags at or above the severity threshold
- **THEN** the SAST check fails and the finding is visible on the pull request

#### Scenario: The analysis also runs on a schedule

- **WHEN** the scheduled analysis runs against the default branch
- **THEN** it re-scans the full codebase independently of pull-request activity

### Requirement: Tools the pipeline downloads are integrity-verified

Any executable or archive the CI pipeline fetches at runtime SHALL be obtained in
a way that verifies its integrity — a version-pinned action whose own supply
chain GitHub attests, or an explicit checksum/signature verification of the
downloaded artifact. The pipeline SHALL NOT pipe an unverified network download
directly into a shell or an extraction step.

#### Scenario: A scanner binary is verified before use

- **WHEN** the pipeline needs a third-party scanner that is not already on the runner
- **THEN** it obtains it via a pinned action or verifies the downloaded artifact's checksum before executing it

#### Scenario: No unverified curl-to-shell remains

- **WHEN** the CI workflow definitions are inspected
- **THEN** no step downloads an artifact over the network and executes or extracts it without an integrity check

### Requirement: Deployment to real infrastructure is gated

The workflow that deploys the hosted platform to real cloud infrastructure SHALL
run under a protected deployment environment, so the credential-issuing step is
subject to that environment's protection rules rather than running on any push to
the default branch. The identity used to reach cloud infrastructure SHALL remain
a short-lived, federated credential — never a long-lived stored key.

#### Scenario: The deploy job runs in a protected environment

- **WHEN** the deploy workflow's job configuration is inspected
- **THEN** it declares a deployment environment, and the cloud-credential step runs only within that environment's context

#### Scenario: No long-lived cloud key is stored

- **WHEN** the deploy workflow and repository secrets are inspected
- **THEN** cloud access is obtained through federated short-lived credentials and no static cloud access key is present

