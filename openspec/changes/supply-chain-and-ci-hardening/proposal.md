## Why

ReleaseTwin's pitch is trustworthy test evidence, so its own build and release
pipeline is part of the product's security story — and a pilot customer's review
will ask about it. Today the pipeline has a secret scanner but no dependency
scanning and no SAST, the published GitHub Action defaults to an unpinned
`:latest` CLI image (arbitrary code in a customer's CI with their secrets
mounted), and the deploy job has no environment gate.

## What Changes

- **The published Action defaults to a pinned image.** `integrations/github-action/action.yml`
  no longer defaults `image` to `...cli:latest`; it defaults to a pinned digest
  updated by the release workflow, and warns (or fails, decided in design) when
  given a mutable tag. Docs gain an explicit warning that fork PRs must not be
  granted ingest secrets. **BREAKING** for a consumer relying on the `:latest`
  default — they must now pass a tag or digest.
- **CI runs dependency-vulnerability scanning.** `dotnet list package --vulnerable`
  (transitive) for `.NET` and `npm audit` for `web/`, plus Dependabot config, with
  a merge-blocking gate on new high/critical advisories.
- **CI runs SAST.** CodeQL for C# and JavaScript/TypeScript on PRs and a weekly
  schedule.
- **The gitleaks install is integrity-checked.** Pin to a SHA-pinned action or
  verify the release tarball checksum instead of piping `curl | tar`.
- **The deploy job gets an environment gate.** `deploy-hosted.yml` runs under a
  GitHub `production` environment so the OIDC-to-AWS step is behind branch
  protection / optional required review rather than any push to `main`.

## Capabilities

### New Capabilities
- `supply-chain-assurance`: what the CI pipeline MUST scan before merge
  (secrets — already present, dependency advisories, SAST), the merge-blocking
  contract for new high-severity findings, the schedule for full sweeps, and the
  integrity requirement for tools the pipeline downloads.

### Modified Capabilities
- `ci-pr-integration`: the distributed Action MUST default to an immutable image
  reference and MUST document the fork-PR secret-exposure boundary.

## Impact

- **Code / config:** `integrations/github-action/action.yml`,
  `integrations/github-action/README.md`, `.github/workflows/secret-scan.yml`
  (gitleaks pin), `.github/workflows/deploy-hosted.yml` (environment),
  new `.github/workflows/codeql.yml`, new dependency-scan workflow(s),
  new `.github/dependabot.yml`, `release.yml` (emit/propagate the pinned digest).
- **Process:** a `production` GitHub environment must be created in repo settings
  (manual, one-time — no code path); branch protection review is the maintainer's
  call given solo ownership.
- **No runtime or hosted-API code changes.** No customer-data migration.
- **Out of scope:** SBOM generation and artifact signing (cosign) — note as a
  follow-up if a pilot asks.
