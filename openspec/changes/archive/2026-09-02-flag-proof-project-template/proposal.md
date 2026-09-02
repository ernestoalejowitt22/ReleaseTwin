## Why

Every `flag_proof` case re-declares its entire `control` block — method, URL,
headers, `auth` (the OAuth2 client-credentials exchange), `verify` read-back, and
`known_bad_when`. For a customer proving ten flags against one provider
(LaunchDarkly, a homegrown flag service, Azure App Config), that is ten
near-identical ~15-line blocks whose only real difference is `feature_key`. The
duplication is error-prone (a headers tweak has to land in every file), it makes
the demo look heavier than it is, and it is the single most common piece of
first-run friction reported for flag proof. There is today **no project-level
configuration surface at all** — every case file is fully standalone.

## What Changes

- **A project manifest file** — `releasetwin.yml` discovered at the root of the
  cases directory (a sibling of the case files, alongside the existing
  `fixtures/` directory). Optional; its absence changes nothing.
- **A `flag_proof.control` default in the manifest.** A `flag_proof` case with no
  `control` block inherits the manifest's. A case that declares a `control` block
  **deep-merges** it over the manifest default — key by key, so a case can
  override just `verify` or add one header without repeating the `auth` section.
  `feature_key` and `build_identity` stay per-case and are never in the manifest.
- **Same resolution rules, unchanged.** `${ENV_VAR}` / hosted `project-secrets`
  resolution and the `{{featureKey}}` / `{{state}}` / `{{enabled}}` / `{{token}}`
  substitutions apply to the merged result exactly as they do to an inline
  `control` block today — the manifest is just another source of the same fields,
  and it SHALL NOT contain a literal credential.
- **Clear load-time errors.** A malformed manifest, an unknown key, or a merged
  `control` block that is still incomplete (e.g. no `url`) is rejected naming
  `releasetwin.yml` and the problem, before any case in the batch runs — matching
  the existing malformed-case-file behavior.
- **Docs + one example.** `docs/flag-proof.md` (or equivalent) gains a
  "shared control template" section; `examples/` gains a two-case project that
  uses the manifest.

Not in scope: manifest-level defaults for anything other than `flag_proof.control`
(no shared `evidence`, `preconditions`, or `requires` — those can follow if asked);
multiple manifests / manifest inheritance up the directory tree.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `case-loading`: new requirement — a project manifest file at the cases-directory
  root is discovered and parsed, with clear errors for a malformed manifest; its
  absence is valid and behaves exactly as today.
- `http-flag-control`: the `control` block (and its `auth` / `verify` sub-blocks)
  MAY be supplied by the project manifest and inherited by a case; a case's inline
  `control` block deep-merges over the manifest default; the merged result is
  subject to the same substitution and `${ENV_VAR}` rules, and the same
  failed/ineligible classifications, as an inline block.

## Impact

- **`src/ReleaseTwin.Cli/CaseLoading/`** — new manifest DTO + discovery in
  `CaseFileLoader`; a merge step applied to `FlagProofControlDto` before it
  becomes a `FlagProofControl`. `CaseFileDto` unchanged.
- **Engine core** — untouched. `FlagProofRunner` and the HTTP
  `IFeatureStateController` see an already-merged control spec.
- **Hosted** — untouched; the manifest is a CLI-side authoring convenience and
  `project-secrets` resolution already flows through the loader's injected
  resolver.
- **Docs / examples** — `docs/flag-proof*.md`, `examples/`, and the README
  flag-proof section.
- No change to the run summary, exit codes, or any wire contract.
