## ADDED Requirements

### Requirement: The Action is consumable by a stable major-version reference
The repository SHALL maintain a floating `v<major>` git tag (and a
`v<major>.<minor>` tag) that is updated to point at each verified release, so a
workflow that pins the Action with
`uses: <repo>/integrations/github-action@v<major>` resolves to the latest
release compatible with that major version. While the project is pre-1.0 the
current major is `0` (`@v0`).

The floating tags SHALL be updated only after the release's build-and-test gate
passes — they SHALL never point at an unverified commit.

The Action's documentation SHALL present a fully pinned reference (e.g.
`@v0.2.0`) as the recommended form for CI and the `@v<major>` form as the
convenience alternative, and SHALL state that the Action's `image` input must
reference a publicly pullable registry tag.

#### Scenario: Pinning the Action to the major version resolves
- **WHEN** a workflow uses the Action with `@v<major>` after at least one release of that major version has been published
- **THEN** the reference resolves to that release's commit and the Action runs

#### Scenario: The floating tag tracks a later patch release
- **WHEN** a subsequent patch release of the same major version is published and its build-and-test gate passes
- **THEN** the `v<major>` tag is updated to the new release commit, and a workflow pinned to `@v<major>` picks it up on its next run

#### Scenario: A failed release does not move the floating tag
- **WHEN** a release's build or test suite fails
- **THEN** the `v<major>` and `v<major>.<minor>` tags are left pointing at the previous verified release
