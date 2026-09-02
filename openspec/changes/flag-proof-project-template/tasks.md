## 1. Manifest model + discovery

- [ ] 1.1 Add a `ProjectManifestDto` (`FlagProof.Control` → reuse
      `FlagProofControlDto`) in `src/ReleaseTwin.Cli/CaseLoading/`; strict
      deserialization (unknown key → error).
- [ ] 1.2 In `CaseFileLoader`, resolve the cases-directory root (same root used
      for fixture resolution) and load `releasetwin.yml` from it once per batch;
      absent → no-op.
- [ ] 1.3 Reject a malformed manifest (bad YAML, unknown key, wrong type) with an
      error naming `releasetwin.yml` and the problem, before any case loads.
- [ ] 1.4 Run the manifest's `${ENV_VAR}` references through the existing injected
      resolver at load time; missing var → load error naming `releasetwin.yml`.

## 2. Merge

- [ ] 2.1 Implement deep merge of the case's inline `FlagProofControlDto` over the
      manifest's: scalars + `headers` key-by-key (case wins); `auth` and `verify`
      replaced wholesale when present on the case.
- [ ] 2.2 Apply the merge in the loader before the DTO→`FlagProofControl` record
      conversion, so substitution/resolution run on the merged result unchanged.
- [ ] 2.3 A `flag_proof` case with no inline `control` and a complete manifest
      block loads as if the block were inline.
- [ ] 2.4 A merged block still missing `url` (or another required field) → load
      error naming the case.
- [ ] 2.5 No manifest + no inline `control` + no adapter controller → still
      ineligible, unchanged.

## 3. Tests

- [ ] 3.1 Loader tests: discovery present/absent; malformed manifest; env-var
      resolve + missing.
- [ ] 3.2 Merge tests: full inherit; header add; `verify`-only override; `auth`
      replace; incomplete merged block.
- [ ] 3.3 Flag-proof runner test (or loader→runner integration): two cases share
      one manifest `control` with `{{featureKey}}` in the URL, each targets its
      own key; a manifest-sourced control 500 fails the run with the same
      classification as inline.
- [ ] 3.4 Report engine + CLI test counts.

## 4. Docs + example

- [ ] 4.1 `docs/flag-proof*.md`: new "Shared control template" section — manifest
      location, merge rules, the "added later only fills omitted fields" note.
- [ ] 4.2 `examples/`: a two-case flag-proof project with a `releasetwin.yml`
      holding the shared `control` (+ `auth`), each case just `feature_key`.
- [ ] 4.3 README flag-proof section: one line + link.

## 5. Verify + close-out

- [ ] 5.1 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green —
      report counts.
- [ ] 5.2 `web/` untouched — confirm no `web/` files changed.
- [ ] 5.3 `openspec validate flag-proof-project-template --strict` passes.
- [ ] 5.4 Confirm with the user before archiving.
