## Why

A recent documentation pass across the ReleaseTwin platform (the sibling `releasetwin-platform` repo) produced a Flow Atlas covering both repos, but the engine-specific content — the CLI, execution kernel, flag proof, and adapters — needs its own home here, since this repo has its own `docs/`, its own `openspec/specs/` (already the authoritative source for this content), and its own GitHub wiki to keep in sync. Doing this as a matching `add-engineering-flow-docs` change lets both repos use the same change name for easy cross-reference, even though they're separate OpenSpec stores.

## What Changes

- Add `docs/engineering/overview.md`, a narrative engineering-flow document covering: the CLI's entry points (`init`/`new`/`run`, `--journey`, summary/JUnit output), the `CaseExecutor` execution kernel's ordered pipeline, the flag-proof paired-execution mechanic, and each shipped adapter (Http, AzureDevOps, LaunchDarkly, Ui) — grounded in the actual code, pointing to `openspec/specs/` for the authoritative behavior contract rather than duplicating it.
- Add a GitHub Actions workflow (`.github/workflows/docs-wiki-sync.yml`) that mirrors `docs/**` to this repo's GitHub wiki on push to `main` touching `docs/**`, matching the mechanics already shipped in `releasetwin-platform` (one-directional, generated `Home.md` when `docs/` has none, a "don't edit here" notice on every synced page).
- The wiki itself is not created by this change — same unavoidable manual step as the platform repo: GitHub provisions a wiki's git repo only after a first page is created through the browser UI, and there's no API for that specific step (there is an API to enable the wiki *feature* itself, which this change's tasks do use).

## Capabilities

### New Capabilities
(none — pure documentation + CI tooling, no product behavior changes)

### Modified Capabilities
(none)

## Impact

- **New files**: `docs/engineering/overview.md`, `.github/workflows/docs-wiki-sync.yml`.
- **No product code, CLI behavior, or spec-level behavior changes** — `skip_specs: true` is set in this change's `.openspec.yaml` accordingly.
- **This repo is public (AGPL-3.0 + Adapter Linking Exception)** — the overview doc should stay strictly about the engine's own architecture and already-public spec contracts; nothing about the private platform's internals (billing, ticket-tracker credentials, hosted infrastructure) belongs here.
- **One manual step, unavoidable**: someone with write access needs to open this repo's Wiki tab and save a first page once, before the sync workflow's first run has a `.wiki.git` remote to push to.
