<!-- One logical change per PR. Open an issue first for anything non-trivial. -->

## What & why

<!-- What this changes and the problem it solves. Link the issue: Closes #___ -->

## Paths touched

- [ ] AGPL-3.0 (`src/`, `tests/`, `docs/`, repo root) — engine
- [ ] Apache-2.0 (`examples/`)
- [ ] BSL 1.1 (`hosted/`, `web/`)

## Checklist

- [ ] Commits signed off (`git commit -s`) — DCO, see CONTRIBUTING.md
- [ ] `dotnet test ReleaseTwin.sln` green (if `src/`/`tests/` touched)
- [ ] `web/` lint + typecheck green (if `web/` touched)
- [ ] Spec added/updated under `openspec/changes/` (or `skip_specs: true` justified)
- [ ] No secrets, `.env*`, or real customer data in the diff
