<!-- One logical change per PR. Open an issue first for anything non-trivial. -->

## What & why

<!-- What this changes and the problem it solves. Link the issue: Closes #___ -->

## Paths touched

- [ ] AGPL-3.0 (`src/`, `tests/`, `docs/`, `openspec/`, repo root) — engine
- [ ] Apache-2.0 (`examples/`, `integrations/`)

## Checklist

- [ ] Commits signed off (`git commit -s`) — DCO, see CONTRIBUTING.md
- [ ] `dotnet test ReleaseTwin.sln` green (if `src/`/`tests/` touched)
- [ ] Spec added/updated under `openspec/changes/` (or `skip_specs: true` justified)
- [ ] No secrets, `.env*`, or real customer data in the diff
