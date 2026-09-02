## 1. Manifest model + discovery

- [x] 1.1 Add a `ProjectManifestDto` (`FlagProof.Control` → reuse
      `FlagProofControlDto`) in `src/ReleaseTwin.Cli/CaseLoading/`; strict
      deserialization (unknown key → error) via a second `_strictDeserializer`.
- [x] 1.2 In `CaseFileLoader.LoadAll`, load `releasetwin.yml` from the cases
      directory once per batch (and skip it from the case-file glob); absent →
      no-op.
- [x] 1.3 Reject a malformed manifest (bad YAML, unknown key, wrong type) with a
      `CaseFileException` naming `releasetwin.yml`, before any case loads.
- [x] 1.4 `InterpolateControlDtoEnv` resolves the manifest's `${ENV_VAR}`
      references at manifest-load time via the injected resolver; missing var →
      load error naming `releasetwin.yml`.

## 2. Merge

- [x] 2.1 `MergeControl` deep-merges the case's inline `FlagProofControlDto` over
      the manifest's: scalars + `headers` key-by-key (case wins); `auth` / `verify`
      replaced wholesale when present on the case.
- [x] 2.2 Merge applied in `ResolveFlagProof` before `ResolveFlagProofControl`, so
      `{{...}}` substitution and the existing validation run on the merged result.
- [x] 2.3 A `flag_proof` case with no inline `control` + a complete manifest block
      resolves as if the block were inline.
- [x] 2.4 A merged block still missing `url`/`method` → `CaseFileException` naming
      the case (existing checks in `ResolveFlagProofControl`).
- [x] 2.5 No manifest + no inline `control` → `MergeControl(null, null)` → null →
      `FlagProofDeclaration` Control stays null, still ineligible, unchanged.

## 3. Tests

- [x] 3.1 Loader tests (`CaseFileLoaderManifestTests`): discovery present/absent;
      manifest not loaded as a case; unknown key; invalid YAML; env-var missing.
- [x] 3.2 Merge tests: full inherit; header add + `verify` add keeping manifest
      url/auth/base-headers; `auth` replace; incomplete merged block → names case.
- [x] 3.3 `OneManifestTemplateServesCasesWithDifferentFlagKeys` +
      `ManifestSourcedControlIsIdenticalToTheEquivalentInlineBlock` — proves the
      runner input is byte-identical to inline, so ControlFailed etc. are
      unchanged (runner has no manifest knowledge).
- [x] 3.4 CLI tests 135 → 147 (+12 `CaseFileLoaderManifestTests`); engine
      (Core.Tests) unchanged at 49. Full `dotnet test ReleaseTwin.sln` green
      (49/29/12/10/5/147/13 across the seven suites).

## 4. Docs + example

- [x] 4.1 `docs/flag-proof.md`: new "Shared control template (`releasetwin.yml`)"
      section — location, merge rules, the "only fills omitted fields" note.
- [x] 4.2 `examples/cases-flag-proof-shared-control/` — `releasetwin.yml` + two
      cases (`checkout-flag` inherits whole; `search-flag` adds a `verify`).
      Covered by `ShippedSharedControlExampleLoads…` test.
- [x] 4.3 README flag-proof section: shared-template line + link; "What's not
      built yet" bullet updated (template + verify no longer deferred).

## 5. Verify + close-out

- [x] 5.1 `dotnet build ReleaseTwin.sln` (0 warnings/errors) + `dotnet test
      ReleaseTwin.sln` green — 265 tests across 7 suites.
- [x] 5.2 `web/` untouched — changes are `src/ReleaseTwin.Cli/CaseLoading/`,
      `tests/ReleaseTwin.Cli.Tests/`, `examples/`, `docs/flag-proof.md`, `README.md`.
- [x] 5.3 `openspec validate flag-proof-project-template --strict` passes.
- [x] 5.4 Confirm with the user before archiving.
