## Context

See proposal.md — Why. Today `src/ReleaseTwin.Cli/CaseLoading/` loads each case
file in isolation; there is no project-level surface. `FlagProofControlDto`
(method/url/headers/body/known_bad_when/auth/verify) is the duplicated shape.
`CaseFileLoader` already takes an injected `resolveEnvironmentVariable` that also
covers hosted `project-secrets`, so credential resolution needs no new plumbing.

## Goals / Non-Goals

**Goals:**
- One place to declare a shared flag-proof `control` block for a suite of cases.
- Zero behavior change for any suite without a `releasetwin.yml`.
- The merged block is indistinguishable from an inline block to the runner.

**Non-Goals:**
- Manifest defaults for anything but `flag_proof.control` (evidence,
  preconditions, requires) — additive later if asked.
- Manifest inheritance up a directory tree / multiple manifests.
- A hosted-project equivalent of the manifest — this is CLI-side authoring only.

## Decisions

### The manifest is `releasetwin.yml` inside the cases directory
It sits beside the case files (`_casesDirectory`), the one path `LoadAll` has
cleanly — `_fixturesRoot` can be overridden independently, so "parent of
fixtures" is not a reliable anchor. `releasetwin.yaml` is also accepted so a
`.yaml` house style is not a silent no-op. **Alternative rejected:** a
`.releasetwin/` directory or a path passed on the CLI — more surface, and the
single-file form matches how small suites are laid out in `examples/`.
**Alternative rejected:** project root next to `fixtures/` — not a directory the
loader is given directly.

### Deep merge, case-over-manifest, with sub-block replace for `auth`/`verify`
Scalars and `headers` merge key-by-key so a case can add one header. `auth` and
`verify` replace wholesale — a half-merged OAuth section (manifest `client_id` +
case `token_url`) is a footgun, and a case that wants a different exchange wants
all of it. **Alternative rejected:** replace-only (a case's `control` wipes the
manifest) — loses the "override just `verify`" ergonomics that motivate the
change. **Alternative rejected:** full recursive merge including `auth` — the
footgun above.

### Merge happens in the loader, before `FlagProofControl` is constructed
The runner, `FlagProofRunner`, and `HttpFeatureStateController` receive an
already-merged spec and stay untouched. The merge operates on the DTO layer
(`FlagProofControlDto`), then the existing DTO→record path runs unchanged, so
`${ENV_VAR}` resolution and `{{...}}` substitution apply to the merged result for
free.

### Validation is load-time and fail-closed
A malformed manifest, an unknown key, or a merged block still missing `url` is a
load error naming `releasetwin.yml` (or the case) — no case in the batch runs.
Matches the existing malformed-case-file contract.

## Risks / Trade-offs

- **A manifest silently changes a case's behavior when added later.** → The
  manifest only supplies fields a case omits; a case with a complete inline
  `control` is unaffected. Documented in the flag-proof docs.
- **Unknown-key strictness breaks forward compat if the manifest grows.** →
  Acceptable now (one section); revisit with a `version` key if the manifest
  gains scope.
- **`examples/` and docs drift.** → One example added in this change; the
  flag-proof doc section is a task.

## Migration Plan

Purely additive. Existing suites keep working with no `releasetwin.yml`. A team
adopting it moves the shared `control` fields out of one case, deletes them from
the rest, and confirms the run is unchanged.

## Open Questions

_Resolved during implementation:_

- `known_bad_when` merges as a scalar like the other fields (case value wins).
- Both `releasetwin.yml` and `releasetwin.yaml` are accepted (`.yml` wins if both
  exist); error messages use the `.yml` spelling.
