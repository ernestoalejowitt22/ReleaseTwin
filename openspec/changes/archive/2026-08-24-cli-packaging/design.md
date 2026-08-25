## Context

See `proposal.md` - Why. Relevant existing shape:

- `src/ReleaseTwin.Cli/Program.cs` takes a single positional argument (the cases directory, default `"cases"`) and reads the *entire* process environment unconditionally — no allowlist, no special parsing (`Program.cs:3-8`).
- `CaseFileLoader` (`src/ReleaseTwin.Cli/CaseLoading/CaseFileLoader.cs:25`) defaults the fixtures root to `casesDirectory/../fixtures` — cases and fixtures are always **siblings under one parent directory**, never nested. A path-traversal guard (`CaseFileLoader.cs:214-222`) rejects any fixture locator that escapes that fixtures root.
- The CLI only ever reads `cases/`/`fixtures/`; results go to `Console.Out`, nothing is written back to those directories.
- `.github/workflows/ci.yml` and `hosted-ci.yml` already build and test on push-to-`main`/PR. Neither publishes anything. This design adds a new, separate tag-triggered workflow rather than standing up CI from scratch.

## Goals / Non-Goals

**Goals:**
- A customer can run the CLI via `docker run` against their own case files with zero local .NET install.
- The container's invocation and exit-code behavior are a drop-in replacement for `dotnet run --project src/ReleaseTwin.Cli -- <dir>` for CI-gating purposes.
- Releases are versioned and reproducible from a git tag.

**Non-Goals:**
- `dotnet tool`/NuGet publishing (deferred to a follow-up change).
- Any file-based output (e.g. JUnit XML) from the CLI — it still only writes to stdout; no writable mount is needed.
- Multi-directory / multi-project invocations in one container run — same one-directory-per-run shape as today.

## Decisions

**Registry: GHCR, not Docker Hub or a third-party registry (e.g. JFrog).** No new vendor account is needed — auth in CI is the built-in `GITHUB_TOKEN` (via `packages: write` permission), matching the repo's existing GitHub-centric setup. Docker Hub's anonymous/free-tier pull rate limits risk breaking a customer's CI; a dedicated artifact platform like JFrog Artifactory is enterprise infrastructure for many artifact types across many teams, not justified by one image from one repo, and cuts against the project's existing "don't build ahead of real need" pattern (see `docs/installation-model.md` on adapters).

**Volume mount: the shared parent directory, read-only.** Because `cases/` and `fixtures/` are always siblings (`CaseFileLoader.cs:25`), mounting only `cases/` would break every fixture resolution. Mounting the parent (`-v $(pwd)/releasetwin:/workspace:ro`) preserves the exact relative-path relationship `CaseFileLoader` already assumes — no path-translation logic is needed inside the CLI or the image. Read-only is safe because the CLI never writes to either directory, and it reinforces the project's existing "immutable fixtures" framing.

**Env var passthrough: both `--env-file` and bare `-e VAR`, documented as alternatives.** `Program.cs` already reads the whole environment unconditionally, so no new parsing is needed on the CLI side — this is purely a documentation/invocation concern. `--env-file` matches how a customer likely already manages local secrets (e.g. a `.env` next to their case files); bare `-e VAR` (host-value passthrough, no `=value`) fits CI systems where secrets are already exported as environment variables by the CI platform itself.

**Entrypoint/CMD layering.**
```dockerfile
ENTRYPOINT ["dotnet", "ReleaseTwin.Cli.dll"]
CMD ["/workspace/cases"]
```
`docker run <image>` runs against the documented convention (`/workspace/cases`) with no arguments needed. `docker run <image> /workspace/other-dir` overrides `CMD` entirely, preserving the same override capability the positional CLI argument already has today. No trade-off between "friendly default" and "override capability" — standard use of the two directives together gives both.

**Base image: multi-stage `dotnet/sdk:8.0` → `dotnet/runtime:8.0`.** `ReleaseTwin.Cli` is framework-dependent, not self-contained, so the final image needs the .NET runtime present. Using `dotnet/runtime` (not `dotnet/aspnet`) keeps the final image minimal — this is a console app, not a web server.

**Multi-arch: `linux/amd64` + `linux/arm64`.** Low marginal cost via `docker/build-push-action`'s `platforms` input; avoids friction for anyone smoke-testing locally on Apple Silicon while CI runners are typically amd64.

**Tagging: semver from the git tag, plus `latest`; document discouraging `latest` in CI.** Release workflow strips a leading `v` from the trigger tag (`v0.1.0` → image tag `0.1.0`) and also pushes `latest`. Docs should actively steer customers toward pinning a specific version in their own CI — an unannounced image update silently changing a customer's CI gate would undercut the product's core pitch (reliably catching regressions), the same "explicit over silent" instinct behind the AzDO adapter's all-or-nothing env var rule.

**Release workflow: new `release.yml`, triggered on `v*.*.*` tag push, re-runs build+test before publishing.** Kept separate from `ci.yml` rather than extending it, since the trigger (tag push) and job (publish) are both distinct from "gate a merge." Re-running restore/build/test in the release job (rather than trusting that a tag was cut from an already-green `main`) is cheap insurance against a tag pointing at a stale or off-`main` commit — a mistagged release publishing a broken image would be a worse outcome than the extra ~1-2 minutes of CI time.

**Scope: Docker only in this change.** A `dotnet tool`/NuGet path is deferred to a small follow-up once the Docker path is proven — see `proposal.md`.

## Risks / Trade-offs

- [Re-running tests in the release job duplicates `ci.yml`'s work] → Accepted; the cost is minutes of CI time, the alternative risk is publishing an unverified release.
- [`latest` tag could still get pinned by a customer despite documentation] → Mitigated by docs only, not enforced technically; acceptable for a pre-pilot product with no customers yet to break.
- [No writable mount today, but a future structured-output feature (e.g. JUnit XML for CI reporting) would need one] → Not building for a hypothetical; noted here so it isn't a surprise when/if that feature is proposed.
- [Multi-arch builds increase release job duration] → Acceptable one-time cost per release; not a per-run cost for customers.

## Open Questions

(none — all decisions needed to write specs/tasks were resolved above)
