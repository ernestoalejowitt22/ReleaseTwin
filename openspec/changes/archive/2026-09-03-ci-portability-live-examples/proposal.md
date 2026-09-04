## Why

`docs/ci.md` documents Bitbucket Pipelines, Azure Pipelines, CircleCI, and Jenkins
snippets for consuming the CLI's `--junit-xml` output, and the GitLab CI/CD
Component has its own `integrations/gitlab-component/` package — but none of the
non-GitHub snippets have ever run against a real pipeline. They're typed, not
proven. Two OSS-funnel changes (`express-flag-proof-example`,
`spa-ui-adapter-ergonomics`) each deliberately deferred their bundled demo app's
CI job here rather than duplicate the work: the funnel needs one place that
proves "clone this, and see release-proof testing gate a real PR" on more than
one CI platform, not three copies of the same claim.

**Known blocker, named up front:** running a live Bitbucket Pipeline or Azure
Pipeline needs accounts on those platforms. Neither currently exists (checked
2026-09-03 — zero Bitbucket/Azure secrets or variables in either the `ReleaseTwin`
or `releasetwin-platform` repo). That account setup is a manual, user-only step;
this proposal scopes the repo and CI wiring so implementation is ready to run the
moment those accounts exist. GitHub Actions needs no new account and can be
built and verified now.

## What Changes

- **New repo `releasetwin-ci-examples`** (public, Apache-2.0 — same license as
  `examples/` in the engine repo, since the whole point is "clone and adapt").
  Holds the runnable demo apps and their CI config; the engine repo keeps only
  the reference case YAML (`examples/cases-express/`, `examples/cases-spa/`,
  already shipped) and narrative docs (`docs/express.md`, `docs/spa-testing.md`).
- Add `apps/express-demo/` — the ~40-line Express app with one behaviour bug
  gated behind one feature flag plus a local flag-toggle endpoint, as scoped in
  `express-flag-proof-example`'s deferred item.
- Add `apps/react-demo/` and `apps/angular-demo/` — the two minimal bundled SPAs
  (one screen, one client-side route change, one rendered value) as scoped in
  `spa-ui-adapter-ergonomics`'s deferred item.
- Add three CI pipeline configs, each running the same story (boot the demo app,
  run its ReleaseTwin case via the published `ghcr.io/…/releasetwin/cli` image,
  fail the pipeline on a case failure or adverse flag-proof verdict):
  - `.github/workflows/*.yml` (GitHub Actions) — buildable and runnable now, no
    new account needed.
  - `bitbucket-pipelines.yml` (Bitbucket Pipelines) — **blocked on a Bitbucket
    account**; write and validate the YAML now, first live run waits on the
    account.
  - `azure-pipelines.yml` (Azure Pipelines) — **blocked on an Azure DevOps
    account**; same treatment.
- Add a top-level `README.md` in the new repo: what each demo app + pipeline
  proves, badges once each pipeline has run at least once.
- `docs/ci.md` in the engine repo gains a "these are proven, not just typed"
  pointer to `releasetwin-ci-examples` once at least one non-GitHub pipeline has
  a real green run — deferred to a follow-up task, not blocking this change's
  GitHub Actions portion.

## Capabilities

### New Capabilities

(none — this change is examples + CI wiring in a separate repo, no engine
behavior changes)

### Modified Capabilities

(none)

## Impact

- New repo `releasetwin-ci-examples` (not yet created).
- No changes to `ReleaseTwin.sln`, `src/`, or any main spec.
- `docs/ci.md` gains a small pointer once the new repo has a real run to point
  at.
- Depends on the already-published `ghcr.io/ernestoalejowitt22/releasetwin/cli`
  image (public, confirmed working — see `v0.2.0` release).
- Bitbucket Pipelines and Azure Pipelines verification is blocked on the user
  creating accounts on those platforms; GitHub Actions verification is not.
