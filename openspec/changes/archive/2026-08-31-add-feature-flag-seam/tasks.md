## 1. Registry & CI parity

- [x] 1.1 Create `flags.json` at the repo root with the entry schema from design D2
  (`key`, `type`, `default`, `description`, `surfaces`, `owner`) and one entry:
  `flag-seam-smoke` (boolean, default `true`, surfaces `["web","hosted","cli"]`).
- [x] 1.2 Write `docs/feature-flags.md`: registry entry format, per-surface read API,
  per-surface local override, naming convention (design D6), and the exact
  provider-adoption steps (design "Migration Plan").
- [x] 1.3 Add a CI step (script under `scripts/` or a small test) that validates
  `flags.json`: parses, required fields present, `default` matches `type`, `surfaces`
  values known, keys kebab-case. Wire it into the existing CI workflow.

## 2. Web (`web/`)

- [x] 2.0 Set up Vitest + React Testing Library in `web/` (`vitest`, `@vitejs/plugin-react`,
  `@testing-library/react`, `@testing-library/jest-dom`, `jsdom`; `vitest.config.ts`;
  `test` + `test:run` scripts; wire `test:run` into CI).
- [x] 2.1 Add `@openfeature/server-sdk`, `@openfeature/web-sdk` to `web/package.json`.
- [x] 2.2 Add a build-time import/copy of root `flags.json` into `web/` (e.g. a linked
  module or `next.config` alias) so the seed and the parity test read the same file.
- [x] 2.3 `web/src/lib/flags.ts`: register an in-memory provider seeded from `flags.json`
  with per-key `FLAG_<KEY>` env overrides; export typed server-side accessors
  (`getFlag(key, ctx)`) and the evaluation-context builder from Clerk `auth()`.
- [x] 2.4 Client-side: a provider component wrapping the web SDK (normal client
  component, no inline `<script>` — see design Risks) and a `useFlag` hook; mount it in
  the dashboard layout, build context from the Clerk client hook.
- [x] 2.5 Read `flag-seam-smoke` once on a server component and once on a client
  component (log or render to a debug-only element) to prove both paths.
- [x] 2.6 Vitest/RTL test: unknown key returns coded default; provider error returns
  coded default; parity test asserts every key referenced in `web/` exists in
  `flags.json` with matching type.
- [x] 2.7 `npm run build` + `npx eslint` clean.

## 3. Hosted API (`hosted/`)

- [x] 3.1 Add the `OpenFeature` NuGet package to `ReleaseTwin.Hosted.Api`.
- [x] 3.2 Embed root `flags.json` as a linked/compiled resource in the project.
- [x] 3.3 `Flags/` folder: `IFlagService` + implementation over the OpenFeature client;
  in-memory provider seeded from the embedded registry; `appsettings` `FeatureFlags:`
  section + `FEATUREFLAGS__<KEY>` env overrides.
- [x] 3.4 Evaluation-context builder from the authenticated principal (org, plan,
  project from the route); `surface` = `hosted`, `env` from hosting environment.
- [x] 3.5 Register `IFlagService` as a singleton in `Program.cs`; confirm no streaming /
  background thread is started (Lambda-safe, design D3).
- [x] 3.6 Read `flag-seam-smoke` in one endpoint path (structured log line only).
- [x] 3.7 Tests: fail-open on provider error, unknown-key default, appsettings/env
  override wins, context shape, registry parity. Report the new test count.

## 4. CLI / engine (`src/`)

- [x] 4.1 Add the `OpenFeature` NuGet package to `ReleaseTwin.Core` (note for the
  licensing review — Apache-2.0) and reference it from `ReleaseTwin.Cli`.
- [x] 4.2 Embed root `flags.json` as a linked/compiled resource.
- [x] 4.3 `IFlagService` for the CLI: in-memory provider seeded from the embedded
  registry, no network; overrides from a new `featureFlags:` map on `releasetwin.yaml`.
- [x] 4.4 Extend `ReleaseTwinConfig` (+ schema/validation) with the `featureFlags` map;
  decide unknown-key behavior (design Open Question — warn vs ignore).
- [x] 4.5 Evaluation-context builder from `releasetwin.yaml` org/project + project API
  identity; `userId` absent; `surface` = `cli`.
- [x] 4.6 Read `flag-seam-smoke` once during a CLI run (verbose/debug output only).
- [x] 4.7 Tests: offline run resolves all flags, `releasetwin.yaml` override wins,
  fail-open, unknown key default, registry parity. Report the new test count.

## 5. Verification & wrap-up

- [x] 5.1 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green; report
  total test count and delta.
- [x] 5.2 `web/` `npm run build` + `npx eslint` green.
- [x] 5.3 CI parity/lint step passes on a dry run.
- [x] 5.4 `openspec validate add-feature-flag-seam --strict` passes.
- [x] 5.5 Confirm no LaunchDarkly package, secret, Terraform, or Vercel env change was
  introduced; update `docs/feature-flags.md` if any detail shifted during
  implementation.
