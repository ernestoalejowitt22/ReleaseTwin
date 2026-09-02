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

### The manifest is `releasetwin.yml` at the cases-directory root
It sits beside the case files and the `fixtures/` directory, so discovery is
"look one level up from a case file" — the same root the loader already computes
for fixture resolution. **Alternative rejected:** a `.releasetwin/` directory or
a path passed on the CLI — more surface, and the single-file form matches how
small suites are laid out in `examples/`.

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

- Should `known_bad_when` be mergeable independently, or is it "all or nothing"
  with the rest of the scalars? (Leaning: it is a scalar, merges like the others.)
- Do we want a `releasetwin.yaml` spelling accepted too, or exactly one name?
  (Leaning: exactly `releasetwin.yml`, error on the other with a hint.)
