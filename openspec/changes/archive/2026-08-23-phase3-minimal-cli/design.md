## Context

See proposal.md - Why. This design covers the YAML case format, how fixtures resolve safely, how the CLI selects adapters for this minimal slice, and what's deliberately left out.

## Goals / Non-Goals

**Goals:**
- A case file format expressive enough to exercise everything already built (fixture integrity, prerequisites, ordered pipeline, cleanup, resource keys, capability requirements) without inventing anything the core doesn't already support.
- Safe fixture loading, reusing an established path-containment pattern (flagged as correctly out-of-core-scope in Phase 1's fit-check, Gap 2 — this change is the component that gap was waiting for).
- A CI-usable exit code and readable console output.

**Non-Goals:**
- Operation parameterization (proposal.md - What Changes). Cases select operations by name; the operations themselves are fixed by adapter construction.
- A generic, pluggable adapter-loading mechanism. With exactly one real adapter (Azure DevOps), a discovery/plugin system would be designed against a sample size of one — deferred until a second real adapter exists to design it against, same reasoning Phase 1/2 already applied to the core itself.
- Machine-readable (JSON) report output. `CaseReport` is already a plain record and trivially serializable later; not needed for a design partner reading console output today.
- Any packaging (npm/NuGet/Docker/GitHub Action) — this is a `dotnet run` / local-build CLI only.

## Decisions

### D1: YAML case format — a trimmed subset of an early illustrative syntax
an early design note already sketched an illustrative case format. This change implements the subset that maps onto capabilities that actually exist:

```yaml
id: CLM-042
oracle:
  locator: tickets/CLM-042
fixture:
  locator: claims/CLM-042.json
  sha256: 8b7d...
requires:
  - http:azure-devops
preconditions:
  - check: azdo.areaPathExists
    owner: QA
pipeline:
  - operation: azdo.createWorkItem
  - operation: azdo.getWorkItem
cleanup:
  - operation: azdo.deleteWorkItem
resource_key: TeamProject\Area
```

Omitted from that early illustrative syntax: `assertions:` (would require operation parameterization — out of scope, see Goals/Non-Goals) and `external_checks:` (Playwright connector — not built, later phase). `requires:` maps directly to `TestCase.RequiredCapabilities`, which already exists in Core.

### D2: Adapter selection is hardcoded for this slice
`Program.cs` registers exactly the Azure DevOps adapter, configured from environment variables (`AZDO_ORG`, `AZDO_PROJECT`, `AZDO_PAT`, plus an area path and variable-group ID). No config-driven adapter selection yet. Alternative considered: a generic `adapters.yaml` naming which adapters to load — rejected as premature given there's only one real adapter to select between; revisit once a second real adapter (Phase 2's own suggestion of eventually adding GitHub/Bitbucket) exists.

### D3: Fixture root and path containment
Fixtures resolve relative to a `fixtures/` directory alongside the cases directory (mirroring an established `payloads/` convention). The loader rejects any locator containing `..` or an absolute path before touching the filesystem — the same defense a prior file-backed payload store already proved necessary (Phase 1 fit-check, Gap 2).

### D4: Output is console text + exit code only
Print each case's pass/fail and classification (if failed) as it completes, then a one-line summary (`N passed, M failed`). Exit code: 0 if all passed, 1 otherwise. No JSON report in this slice — `CaseReport` is already serializable, so adding a `--json` flag later is a small, backward-compatible addition, not a redesign.

## Risks / Trade-offs

- **[Risk] Hardcoded adapter selection (D2) means this CLI can't yet be handed to someone who isn't testing against Azure DevOps.** → Mitigation: explicitly the point — a design partner's own workflow will need its own adapter anyway (see proposal.md's stated scope limit); building generic adapter loading before that concrete need exists would be guessing at its shape.
- **[Risk] No operation parameters means real design-partner cases can't actually be authored yet, only demoed against Azure DevOps.** → Mitigation: stated explicitly in the proposal so it isn't discovered as a surprise; the point of this slice is proving the load→compose→execute→report loop works end to end, not delivering a partner-ready product.

## Open Questions

None — the format, adapter-selection mechanism, and output shape were all decided above rather than deferred.
