## 1. `dotnet tool` packaging

- [x] 1.1 Add tool + package metadata to `src/ReleaseTwin.Cli/ReleaseTwin.Cli.csproj`:
      `PackAsTool=true`, `ToolCommandName=releasetwin`, `PackageId=releasetwin`,
      `PackageLicenseExpression=AGPL-3.0-only WITH LicenseRef-ReleaseTwin-Adapter-Exception`
      (matching the engine), `Description`, `Authors`, `PackageProjectUrl`,
      `RepositoryUrl`, `PackageReadmeFile` (a short `src/ReleaseTwin.Cli/README.md`).
- [x] 1.2 `dotnet pack src/ReleaseTwin.Cli -c Release` locally — confirm a
      `.nupkg` is produced and `dotnet tool install --global --add-source ./nupkg releasetwin`
      then `releasetwin examples/cases-http-only` runs green (fixture resolution,
      exit code) identical to `dotnet run`.
- [x] 1.3 `releasetwin --help` / no-arg behavior and `${ENV_VAR}` interpolation
      verified through the installed tool against `examples/`.
- [x] 1.4 Add `src/ReleaseTwin.Cli/README.md` (tool page): install line, the
      Docker/`dotnet tool` parity note, the `playwright install` note for UI
      journeys, link to `docs/install.md`. SPDX header per `REUSE.toml`.
- [x] 1.5 `reuse lint` green after the new files.

## 2. Release automation

- [x] 2.1 `.github/workflows/release.yml`: after the test step, add
      `dotnet pack src/ReleaseTwin.Cli -c Release -o ./artifacts` and
      `dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }}
      --source https://api.nuget.org/v3/index.json --skip-duplicate`.
- [x] 2.2 Guard the push so a missing `NUGET_API_KEY` fails the release loudly
      (not silently skips) — the secret is required once manual step 1 is done.
- [x] 2.3 Add a floating-tag step to `release.yml`: derive `v<major>` and
      `v<major>.<minor>` from `GITHUB_REF_NAME`, `git tag -f` both,
      `git push -f origin <tag>` — only reached if build+test+publish succeeded.
- [x] 2.4 Bump the release job `permissions:` from `contents: read` to
      `contents: write` (needed for the tag push; scoped to this job).
- [x] 2.5 Dry-run reasoning / comment in the workflow: what each new step does,
      and that the force-push is limited to the two derived tags.

## 3. GitHub Action consumability

- [x] 3.1 `integrations/github-action/README.md`: recommend a fully pinned
      `@vX.Y.Z`; document `@v0` as the convenience ref; state the `image` input
      must be a publicly pullable tag (and that this repo's image becomes
      public via `go-public-sequence`).
- [x] 3.2 `integrations/github-action/action.yml`: update the `image` default
      comment; no behavior change.
- [x] 3.3 `docs/ci.md`: replace the literal `OWNER` / `VERSION` placeholders
      with the real repo and a real pinned version; add the `uses: …@v0`
      Action recipe alongside the raw `docker run` one; add the "run-only gate"
      recipe (`comment: false`, make the check required).

## 4. Doc reconciliation

- [x] 4.1 New `docs/install.md`: the three paths side by side — Docker (no
      SDK), `dotnet tool` (have .NET), the GitHub Action (in CI) — each with a
      copy-paste snippet and when to use it. Link from `quickstart.md`.
- [x] 4.2 `docs/installation-model.md`: drop "`dotnet tool`/NuGet and a GitHub
      Action wrapper are still deferred" — state all three exist; Homebrew /
      single-file binary remain deferred.
- [x] 4.3 `README.md` "What's not built yet": update the packaging bullet to
      match — Docker + `dotnet tool` + Action done, Homebrew deferred.
- [x] 4.4 `docs/ideas/deferred-backlog.md` item 1: narrow it to just Homebrew /
      single-file binary, or mark the CLI-packaging item resolved.

## 5. Verify + close-out

- [x] 5.1 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green
      (no code changed, but the csproj did) — report counts.
- [x] 5.2 `web/` unaffected — no build needed; confirm no `web/` files touched.
- [x] 5.3 `openspec validate cli-distribution --strict` passes.
- [ ] 5.4 Confirm with the user before archiving.

## 6. Needs the user to run this

- [ ] 6.1 Create a **nuget.org** account; generate a push API key (glob
      `releasetwin` or `ReleaseTwin.*`); add it as the `NUGET_API_KEY` **repo
      secret** (Settings → Secrets and variables → Actions → Secrets).
- [ ] 6.2 Claim the `releasetwin` package id: either reserve the prefix on
      nuget.org, or run one `dotnet nuget push` interactively with the key
      before the first CI release.
- [ ] 6.3 Cut the next version tag (e.g. `v0.2.0`) to trigger the first
      combined release; then `dotnet tool install -g releasetwin` on a clean
      machine and a scratch repo `uses: …@v0` to confirm both.
- [ ] 6.4 After `go-public-sequence`: set the GHCR `cli` package to public and
      publish the Action to the GitHub Marketplace from the Release page.
