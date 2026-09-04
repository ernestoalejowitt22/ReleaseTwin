## ADDED Requirements

### Requirement: The PR comment carries an attribution link by default

The GitHub Action's rendered pull-request comment SHALL include a link to the
ReleaseTwin product site as a distinct footer line, separate from the pass/fail
totals and flag-proof verdict, when the Action's `attribution` input is `true` (the
default). The Action SHALL accept an `attribution` boolean input; when it is set
to `false`, the comment SHALL be rendered exactly as it would be with no footer
line. This requirement does not change the check run, which SHALL continue to
carry no attribution content.

#### Scenario: A rendered comment includes the footer link by default

- **WHEN** the Action renders a PR comment and the `attribution` input is not set
- **THEN** the comment body contains a link to the ReleaseTwin product site, positioned after the pass/fail totals and flag-proof verdict content

#### Scenario: A caller can opt out of the footer link

- **WHEN** the Action is invoked with `attribution: false`
- **THEN** the rendered comment contains no product-site link and is otherwise unchanged from the comment the Action would render with attribution enabled

#### Scenario: The check run is unaffected

- **WHEN** the Action renders a summary with the `attribution` input at its default
- **THEN** the check run's body carries no attribution content, regardless of the `attribution` input's value

### Requirement: The Action and GitLab Component READMEs link to the product site

The `integrations/github-action/README.md` and `integrations/gitlab-component/README.md`
files SHALL each contain a link to the ReleaseTwin product site, in addition to
their existing links to `docs/*.md`, so a reader who discovers either integration
independently of the main repository can reach the product site.

#### Scenario: The Action README links to the product site

- **WHEN** `integrations/github-action/README.md` is inspected
- **THEN** it contains a link to the ReleaseTwin product site

#### Scenario: The GitLab Component README links to the product site

- **WHEN** `integrations/gitlab-component/README.md` is inspected
- **THEN** it contains a link to the ReleaseTwin product site
