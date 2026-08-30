## MODIFIED Requirements

### Requirement: Multiple adapters compose in the CLI
The CLI SHALL be able to install more than one adapter into the same composition, so a case can reference operations from any installed adapter. An adapter that requires no credentials (e.g. a generic HTTP adapter) SHALL install successfully without any credential environment variables being set.

The set of adapters the CLI considers MAY be declared in an optional `releasetwin.yaml` file at the project root, via an `adapters:` list of adapter names. When the file is absent or has no `adapters:` key, the CLI SHALL consider every adapter it knows about and auto-load each one whose credentials fully resolve — the behavior that existed before this file. When an `adapters:` list is present it is authoritative: only listed adapters are considered, a credential-free adapter (HTTP) is available whether or not it is listed, an adapter configured in the environment but not listed SHALL NOT be installed, and a listed credentialed adapter whose credentials resolve from neither the environment nor a hosted fetch SHALL be reported as a clear startup error rather than silently skipped. The file names which adapters a project uses; it SHALL NOT contain credentials, which continue to resolve only from the environment or the hosted `adapter-credentials` capability. A `releasetwin.yaml` that is present but malformed SHALL be a startup error.

#### Scenario: Cases from two different adapters run in the same invocation
- **WHEN** the CLI is run with a cases directory containing one case using Azure DevOps operations and one case using generic HTTP operations
- **THEN** both cases execute successfully in the same run, using their respective adapters

#### Scenario: No config file preserves auto-detection
- **WHEN** there is no `releasetwin.yaml` (or it has no `adapters:` key) and an adapter's full credential set is present in the environment
- **THEN** the CLI installs that adapter exactly as it did before this file existed

#### Scenario: A listed adapter with no credentials is a startup error
- **WHEN** `releasetwin.yaml` lists a credentialed adapter and neither the environment nor a hosted fetch provides its credentials
- **THEN** the CLI exits with a clear error naming that adapter, without executing any case

#### Scenario: An unlisted but environment-configured adapter is not installed
- **WHEN** `releasetwin.yaml` has an `adapters:` list that omits an adapter whose credential environment variables are nonetheless fully set
- **THEN** the CLI does not install that adapter, and a case referencing its operations reports the missing-capability outcome

#### Scenario: HTTP is always available
- **WHEN** `releasetwin.yaml` has an `adapters:` list that does not include `http`
- **THEN** the generic HTTP adapter is still installed and `http.*` operations still run
