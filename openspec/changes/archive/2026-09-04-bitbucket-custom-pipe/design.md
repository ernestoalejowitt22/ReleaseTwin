## Context

See proposal.md - Why. `docs/ci.md`'s current Bitbucket snippet is a raw `image:` +
`script:` block; GitHub and GitLab each instead get a declarative, versioned
reference (`uses:` / `include:`). Bitbucket's equivalent is a Pipe: a Docker image
referenced as `pipe: docker://<image>:<tag>` whose declared `variables:` become
environment variables inside the container — there is no orchestration script like
the GitHub Action's composite steps; whatever the image's own entrypoint does with
those env vars *is* the pipe's behavior.

Investigating `src/ReleaseTwin.Cli/CliEntrypoint.cs` turned up something that
shrinks this change: the CLI **already** reads `RELEASETWIN_JUNIT_XML` and
`RELEASETWIN_SUMMARY_JSON` as environment-variable equivalents of `--junit-xml` /
`--summary-json` (added for `ci-pr-integration`/`ci-report-formats`, `CliEntrypoint.cs:60-77`).
The only argument that is *not* also available as an env var is the cases
directory path — it's positional only, defaulting to `cases` when omitted
(`CliEntrypoint.cs:162`). So a wrapper image is needed only to turn one pipe
variable (a custom cases path) into a positional argument; every other value a
caller might want to set already works by pointing the pipe's declared variable
name directly at the CLI's own env var.

## Goals / Non-Goals

**Goals:**
- A Bitbucket Pipelines step can reference the pipe declaratively and get the same
  outcome as today's raw script block, with less to copy and version-pin.
- The wrapper stays minimal — it exists to bridge one positional argument, not to
  reimplement any part of the CLI's argument handling.

**Non-Goals:**
- No new CLI feature. If the CLI ever grows a `RELEASETWIN_CASES_PATH` env var for
  its own reasons, the wrapper's one line of logic becomes redundant and can be
  deleted, but this change does not add that env var to the CLI itself — it's a
  wrapper-only concern, kept out of the core/adapter surface per this repo's
  core/adapter boundary convention.
- No submission to Atlassian's `official-pipes` catalog (see proposal.md).
- No change to `docs/ci.md`'s CircleCI or Azure Pipelines sections.

## Decisions

**A wrapper image, not a bare reference to the published CLI image.** Alternative
considered: document `pipe: docker://ghcr.io/.../cli:<version>` directly, since
`RELEASETWIN_JUNIT_XML` already works as a pipe variable pointed straight at the
CLI's own env var. Rejected as the *only* path — it works when the cases directory
is the CLI's default (`cases`), but silently ignores a caller's `CASES_PATH`
variable otherwise, since the bare CLI image's entrypoint never reads it. A
wrapper that forwards `${CASES_PATH:-cases}` as the positional argument (and
otherwise changes nothing) covers both cases correctly. The bare-image path stays
available as an implementation *detail* callers could still use directly if they
never need a custom path, but the documented, supported form is the wrapper.

**Wrapper variables: only `CASES_PATH` is wrapper-specific.** `RELEASETWIN_JUNIT_XML`,
`RELEASETWIN_SUMMARY_JSON`, and any adapter-credential env vars a case needs are
declared in `pipe.yml` as pass-through variables with the CLI's own names — the
wrapper does not rename or re-derive them. This keeps the wrapper's own logic to
one line and means `pipe.yml`'s variable list documents the CLI's real
env-var contract instead of a pipe-specific dialect of it.

**`JUNIT_XML_PATH` default matches Bitbucket's own default collection glob.**
`pipe.yml` defaults `RELEASETWIN_JUNIT_XML` to `test-results/junit.xml` — inside
the `**/test-results/*.xml` glob `docs/ci.md` already documents Bitbucket scanning
with no configuration key, so the default path requires no additional
`artifacts:` or test-results configuration from the caller, mirroring the
zero-config JUnit ingestion Bitbucket already provides today.

**Release pinning mirrors the GitHub Action's existing pattern.** `.github/workflows/release.yml`
already advances the Action's default image to a freshly-built digest via a
`sed`-replace, gated on build+test+push success (`release.yml:94-126`). The same
release job gains an equivalent step for `pipe.yml`'s image reference — same gate,
same digest-pin mechanism, one more `sed` target — rather than a parallel release
workflow.

## Risks / Trade-offs

- [The wrapper image adds one more artifact to keep in sync with CLI image
  releases] → it is built FROM the just-published CLI image in the same release
  job, immediately after that image is pushed, so it can never reference a stale
  base — mirrors how the Action's pin step already runs only after the CLI image
  push succeeds.
- [A caller references the bare CLI image directly, hits the missing-`CASES_PATH`
  gap, and is confused] → `docs/ci.md` documents the wrapper pipe as the supported
  form; the raw `image:`/`script:` fallback (unchanged, already handles a custom
  path via its own `script:` line) remains the documented alternative for anyone
  who skips the pipe.

## Migration Plan

No migration — purely additive. Existing Bitbucket Pipelines configurations using
today's raw `image:`/`script:` snippet are unaffected; `docs/ci.md` keeps that form
as a fallback rather than replacing it. Ships on the next tagged release once the
new release-workflow step is in place.
