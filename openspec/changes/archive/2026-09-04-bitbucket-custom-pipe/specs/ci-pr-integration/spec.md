## ADDED Requirements

### Requirement: A Bitbucket Pipe runs the CLI and produces a native-ingestible JUnit report

The repository SHALL provide an open-source Bitbucket Pipe, under
`integrations/bitbucket-pipe/`, that a Bitbucket Pipelines step can consume with a
single `pipe:` reference. The pipe SHALL run the ReleaseTwin CLI over a
caller-specified cases path (supplied via a declared pipe variable, defaulting to a
documented path when omitted) and write a JUnit report to the path Bitbucket
Pipelines scans by default, so Bitbucket's built-in test-results collection
populates from the run with no `artifacts:` configuration.

The pipe SHALL gate the job the way the CLI's own exit code does — a case failure
or an adverse flag-proof verdict SHALL fail the pipeline step, so the pipe can be
used as a required check on the target branch.

The pipe SHALL require only credentials the caller supplies for the run itself
(flag-source credentials, an optional hosted API token, forwarded via Bitbucket
pipeline variables). It SHALL NOT require a ReleaseTwin account or hosted call to
produce the test results.

The pipe's source SHALL be licensed Apache-2.0, independently of the engine's
copyleft license, consistent with `integrations/github-action/` and
`integrations/gitlab-component/`.

The pipe SHALL be usable by a direct `pipe: docker://<image>:<tag>` reference to
its published image; listing in Atlassian's `official-pipes` catalog is an
optional, separately-pursued step and SHALL NOT be a precondition for the pipe
working.

#### Scenario: The pipe produces Bitbucket-native test results

- **WHEN** a Bitbucket Pipelines step uses the pipe pointed at a cases directory
  and the pipeline runs
- **THEN** a JUnit report is written to the path Bitbucket Pipelines scans by
  default, and Bitbucket's test-results view shows one result per case, with
  failures reflecting the CLI's outcomes and flag-proof verdicts (including a
  flag-proof case that could not be paired)

#### Scenario: A failing run fails the step

- **WHEN** the CLI run under the pipe has any case failure or an adverse
  flag-proof verdict
- **THEN** the pipe's step exits non-zero

#### Scenario: The pipe needs no ReleaseTwin credentials for test results

- **WHEN** the pipe's declared variables are inspected
- **THEN** producing Bitbucket's test-results view requires only the JUnit report the pipe writes, with no ReleaseTwin API token or hosted endpoint referenced

#### Scenario: The pipe is usable before any catalog listing

- **WHEN** the pipe's published image exists but it has not been submitted to (or accepted into) Atlassian's `official-pipes` catalog
- **THEN** a Bitbucket Pipelines step can still reference it directly via `pipe: docker://<image>:<tag>` and it runs correctly

#### Scenario: The pipe is Apache-2.0

- **WHEN** the licensing of `integrations/bitbucket-pipe/` is inspected
- **THEN** it is Apache-2.0, separate from the engine's licence

### Requirement: The Bitbucket Pipe's published image is version-pinned and advanced only after verification

The pipe's `pipe.yml` SHALL reference its published wrapper image by an immutable
tag or digest, not a mutable tag such as `latest`. The release process SHALL
advance that pinned reference only after the release's build-and-test gate passes
and the wrapper image has been built and pushed, following the same pattern
`.github/workflows/release.yml` already uses to pin the GitHub Action's default
image reference.

#### Scenario: The pipe's default image is immutable

- **WHEN** `integrations/bitbucket-pipe/pipe.yml` is inspected
- **THEN** its image reference is a pinned version or digest, not `latest`

#### Scenario: A failed release does not move the pinned reference

- **WHEN** a release's build or test suite fails
- **THEN** `pipe.yml`'s image reference is left pointing at the previous verified release

### Requirement: `docs/ci.md` documents the Bitbucket Pipe as the primary Bitbucket path

`docs/ci.md`'s Bitbucket Pipelines section SHALL present the pipe as the
documented snippet, and SHALL retain the existing raw `image:`/`script:` form as a
documented fallback for callers who do not want to pull an additional wrapper
image.

#### Scenario: The docs show the pipe form

- **WHEN** the Bitbucket Pipelines section of `docs/ci.md` is inspected
- **THEN** it contains a working `pipe:`-based snippet, and the prior raw `image:`/`script:` snippet is still present as a fallback
