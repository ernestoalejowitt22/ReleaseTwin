## ADDED Requirements

### Requirement: The published CLI can run browser cases in a third-party CI system

The release artifacts SHALL provide a documented, supported way to run `ui.*`
(browser) cases — not only HTTP and adapter cases — without a source checkout of
the engine, so a pipeline on Bitbucket, Azure Pipelines, GitHub Actions, or a
comparable system can run a UI journey against the published CLI. The supported
way MAY be a browser-capable image variant, bundled browser binaries, or a
documented base-image-plus-tool recipe; whichever form is chosen SHALL be
covered by `docs/`.

#### Scenario: A pipeline runs a UI journey from the release artifact

- **WHEN** a CI pipeline that has not checked out the engine source follows the
  documented recipe to run a case whose pipeline includes `ui.*` steps, with the
  UI adapter enabled
- **THEN** the browser launches and the UI steps execute, producing the same
  outcome and the same JUnit-XML report they would from a source build

#### Scenario: The recipe is pinned to a released version

- **WHEN** the documented recipe names the CLI artifact
- **THEN** it pins an explicit released version (not a floating tag), consistent
  with the existing "pin a released version in CI" guidance
