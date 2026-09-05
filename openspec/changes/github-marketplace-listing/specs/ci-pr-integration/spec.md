## ADDED Requirements

### Requirement: The GitHub Action is also published to a dedicated root-level repository

The repository's GitHub Action (`integrations/github-action/`) SHALL also be
published, unmodified, to a dedicated repository
(`ernestoalejowitt22/releasetwin-action`) whose `action.yml` sits at that
repository's root, so it is eligible for GitHub Marketplace listing — a platform
requirement that a subdirectory-hosted action cannot meet. The dedicated
repository SHALL contain only the Action's own files (`action.yml`, `render.mjs`,
`render.test.mjs`, `README.md`, `LICENSE`) and SHALL be licensed Apache-2.0 only,
so its license (as GitHub displays it on the Marketplace) accurately reflects the
Action's own license rather than this repository's AGPL-3.0 root license.

The dedicated repository SHALL be a publish target only — its content SHALL be
produced by the release process from `integrations/github-action/`'s current
state, never edited directly.

The existing subdirectory reference
(`uses: ernestoalejowitt22/ReleaseTwin/integrations/github-action@<ref>`) SHALL
continue to work unchanged; publishing to the dedicated repository SHALL NOT
deprecate or alter it.

#### Scenario: A tagged release updates the dedicated repository

- **WHEN** a release is tagged and its build-and-test gate passes
- **THEN** `ernestoalejowitt22/releasetwin-action`'s default branch and version
  tag are updated to match `integrations/github-action/`'s content at that release

#### Scenario: The dedicated repository's license matches the Action's own

- **WHEN** the license of `ernestoalejowitt22/releasetwin-action` is inspected
- **THEN** it is Apache-2.0, independent of this repository's AGPL-3.0 root
  license

#### Scenario: The subdirectory reference still works

- **WHEN** a workflow uses
  `uses: ernestoalejowitt22/ReleaseTwin/integrations/github-action@<ref>`
- **THEN** it resolves and runs exactly as it did before this capability existed

#### Scenario: A failed release does not update the dedicated repository

- **WHEN** a release's build or test suite fails
- **THEN** `ernestoalejowitt22/releasetwin-action` is left at its previous verified state
