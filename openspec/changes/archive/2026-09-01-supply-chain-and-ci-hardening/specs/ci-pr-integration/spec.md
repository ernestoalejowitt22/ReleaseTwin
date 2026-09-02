## ADDED Requirements

### Requirement: The Action defaults to an immutable image reference

The published GitHub Action SHALL NOT default its runner image input to a mutable
tag (such as `latest` or a moving major-version tag). Its default SHALL be an
immutable reference — a specific released version or a content digest — that the
release process updates deliberately. When a caller explicitly supplies a mutable
tag, the Action SHALL surface a visible warning in the job log naming the risk;
it SHALL still run, so a caller who accepts the risk is not blocked.

#### Scenario: The default image is immutable

- **WHEN** the Action's input defaults are inspected
- **THEN** the image input defaults to a pinned version or digest, not `latest` or a bare major-version tag

#### Scenario: A caller-supplied mutable tag warns but runs

- **WHEN** a workflow invokes the Action with an image reference that is a mutable tag
- **THEN** the job log contains a warning that the image is not pinned, and the run proceeds

#### Scenario: The release process updates the pinned default

- **WHEN** a new CLI image version is released
- **THEN** the release process updates the Action's default image reference to the new immutable reference

### Requirement: The Action documents the fork-pull-request secret boundary

The Action's documentation SHALL state explicitly that any secrets passed to the
CLI container (via the env-file or forwarded environment variables) are exposed
to the case files it runs, and that a workflow which runs the Action on
pull requests from forks MUST NOT make ingest or other sensitive secrets
available to it.

#### Scenario: The docs carry the fork-PR warning

- **WHEN** the Action's README is inspected
- **THEN** it contains an explicit warning that case files can read any secret handed to the container and that fork-PR workflows must withhold sensitive secrets
