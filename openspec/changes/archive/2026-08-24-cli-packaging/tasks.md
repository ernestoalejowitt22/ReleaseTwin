## 1. Dockerfile

- [x] 1.1 Add a multi-stage `Dockerfile` for `src/ReleaseTwin.Cli`: build stage on `mcr.microsoft.com/dotnet/sdk:8.0` running `dotnet publish`, final stage on `mcr.microsoft.com/dotnet/runtime:8.0` copying the published output.
- [x] 1.2 Set `ENTRYPOINT ["dotnet", "ReleaseTwin.Cli.dll"]` and `CMD ["/workspace/cases"]`.
- [x] 1.3 Verify locally: `docker build` succeeds, and `docker run --rm -v $(pwd)/examples:/workspace:ro <image>` runs the bundled zero-credential HTTP example and exits 0.
- [x] 1.4 Verify the non-zero exit path: run the image against a directory that includes a failing/ineligible case and confirm a non-zero exit code.
- [x] 1.5 Verify fixture resolution: confirm a case referencing a fixture resolves it from the mounted directory's sibling `fixtures/`, not from inside `cases/`.
- [x] 1.6 Verify env var passthrough: run with `--env-file` and separately with bare `-e VAR` (host passthrough), confirming a case using `${ENV_VAR}` interpolation picks up each.
- [x] 1.7 Verify argument override: run with an explicit path argument different from `/workspace/cases` and confirm it takes precedence over `CMD`.

## 2. Release workflow

- [x] 2.1 Add `.github/workflows/release.yml`, triggered on push of tags matching `v*.*.*`.
- [x] 2.2 Add restore/build/test steps (same as `ci.yml`) as a gate before any publish step.
- [x] 2.3 Add a step deriving the release version from the tag (strip leading `v`).
- [x] 2.4 Add GHCR login using the built-in `GITHUB_TOKEN`; grant the workflow `packages: write` permission.
- [x] 2.5 Add a multi-platform (`linux/amd64,linux/arm64`) build-and-push step tagging the image with both the derived version and `latest`.
- [x] 2.6 Confirm a failed build/test step blocks the publish steps (fail-fast, no partial publish) — no step sets `continue-on-error`, so default GitHub Actions fail-stop semantics apply; a failing Restore/Build/Test step halts the job before any Docker step runs.

## 3. Documentation

- [x] 3.1 Update `README.md` with the Docker install/run instructions (pull command, volume-mount shape, env var passthrough, both no-argument and explicit-argument invocations).
- [x] 3.2 Update `docs/installation-model.md`'s "Packaging/distribution" deferred-item entry to reflect Docker as shipped, `dotnet tool` as still deferred.
- [x] 3.3 Document the "pin a version tag in CI, don't rely on `latest`" guidance alongside the install instructions.

## 4. End-to-end verification

- [x] 4.1 Verified via local equivalent (nothing is published yet — see 4.2): built the image from the new `Dockerfile` and ran it against `examples/cases` with no .NET SDK involved in the run; output matched the README's documented example exactly (`FAIL CLM-042 ... / FLAGPROOF FLAGPROOF-DEMO-1 (Ineligible) ... / PASS HTTP-DEMO-1 / 1 passed, 2 failed`, exit code 1). Also verified a clean-pass case (exit 0), fixture resolution from the sibling `fixtures/` directory, both env-passthrough forms, and the argument-override path — see section 1.
- [ ] 4.2 (Deliberately deferred — requires pushing a real git tag and publishing a real GHCR package, a shared/hard-to-reverse action; do this when actually cutting the first real release, not as part of this planning/implementation pass.) Confirm a tagged release actually produces a pullable image at `ghcr.io/<org>/<image>:<version>` end to end (push a test tag, observe the workflow run, pull the result).
