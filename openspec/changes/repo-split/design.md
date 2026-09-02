## Context

One private monorepo today: `src/` (AGPL engine + adapters + CLI), `hosted/`
(BSL API + terraform), `web/` (BSL marketing + dashboard), `integrations/`
(Apache Action), `openspec/` (34 specs + 1 active + 52 archived changes),
`docs/` (23 files, mixed user-facing and ops/legal). `REUSE.toml` maps all four
licenses. The AWS OIDC trust in `hosted/terraform-bootstrap` is scoped to
`repo:ernestoalejowitt22/ReleaseTwin`. Vercel builds `web/` from this repo.
History was `filter-repo`'d twice already (2026-08-30 content, 2026-09-02
messages) and force-pushed; the operator accepts the dangling-SHA residual.

`go-public-sequence` (active) plans to flip this whole repo public.

## Goals / Non-Goals

**Goals:**
- The engine, its specs, and how-to-use-it docs live in one repo that is safe to
  make public — no hosted source, no infra, no planning history, no legal.
- The hosted service, its infra, its planning history, and the company/ops docs
  live in one private repo.
- Both repos keep full (scrubbed) git history for their own paths.
- The eventual public flip is a repo-visibility toggle, nothing more.

**Non-Goals:**
- Changing any license term, engine/API/CLI contract, or behavior (`skip_specs`).
- Squashing or re-dating history.
- Publishing `releasetwin-platform` — it stays private.
- Renaming `ReleaseTwin` or anything a consumer already references.

## Decisions

### Two repos, `ReleaseTwin` stays the engine
The public-facing name, the domain, the (eventual) stars and issues all attach to
`ReleaseTwin`. It keeps the name and is *trimmed down* to the engine.
`releasetwin-platform` is the new private repo. **Alternative rejected:** new
public repo for the engine, `ReleaseTwin` stays private — throws away the
established name and any existing inbound links for no benefit.

### `git filter-repo --path` twice, from mirror clones
Private repo: `filter-repo --path hosted/ --path web/ --path <private docs> …`
(keep-list). Public repo: `filter-repo --invert-paths --path <same private set>`
(remove-list) on a fresh mirror of the current `ReleaseTwin`. Both preserve
history for the paths they keep. **Alternative rejected:** `git subtree split` —
awkward for a many-directory partition and does not handle the root-file
reconciliation. **Alternative rejected:** fresh `git init` for the public repo
(squash) — the operator chose to keep full history.

### Planning splits with the code it plans
`openspec/specs/` and `openspec/changes/archive/` are partitioned by subject:
engine capabilities public, hosted capabilities private. Archived changes are
internal process artifacts — the public repo keeps only `openspec/specs/` for the
engine (a genuine trust signal for a test-evidence product) and no archived
change history. `go-public-sequence` moves to the private repo and is rewritten
there. **Alternative rejected:** keep all `openspec/` public — that is precisely
the roadmap / weakness / commercial-reasoning exposure this change exists to
remove.

### Root files reconciled by hand, not filtered
`ReleaseTwin.sln` (drop hosted projects), `REUSE.toml` (drop BUSL + hosted-path
annotations), `README.md` (rewrite: engine-first, lead with the Adapter Linking
Exception), `CLAUDE.md` (split). These are small, high-touch, and a path filter
cannot edit file *contents* — done as ordinary commits after the filter.

### Infra cutover is sequenced and gated
The private repo is created and populated first, its build verified, **then** the
OIDC trust re-point + secret move + Vercel reconnect, **then** a green
`deploy-hosted` run from the new repo, **then** the public repo is trimmed. If
the private side is not deploying, the public trim does not start — the monorepo
on `main` stays the source of truth until the private repo demonstrably works.

## Risks / Trade-offs

- **Deploy outage window.** Between the OIDC re-point and the first green deploy
  from `releasetwin-platform`, `hosted/` cannot deploy. → Do the cutover in one
  sitting; the dev stack keeps running on its last deploy throughout (no data
  touched). Not customer-visible (no customers yet).
- **A cross-repo reference is missed** and something 404s after the trim. →
  Explicit audit task (`git grep` for `ReleaseTwin/hosted`, `ReleaseTwin/web`,
  `uses: …/ReleaseTwin/`), and the public repo's own `ci.yml` catches a broken
  `.sln`.
- **Third history rewrite.** Same residual as §2, already accepted. Open PRs must
  be closed/merged first (currently: #108 draft, #50 landing-demo — both can be
  merged or closed before the split).
- **`releasetwin-platform` loses the public repo's issue/PR history** for hosted
  work. → Acceptable; that history is thin and internal.
- **Two repos to keep in sync** for anything that spans (e.g. a spec that
  describes an engine↔hosted seam). → Rare; the `ingest-api` / `cli-runner`
  boundary is the only real one, and it is stable.
- **Vercel reconnection** may need a fresh project (build settings, env). →
  Documented as a manual step; `web/` env is small.

## Migration / cutover order

1. Merge or close open PRs (#108, #50).
2. Mirror `ReleaseTwin` → `filter-repo --path` (private keep-list) → push to new
   private `releasetwin-platform`. Reconcile its root files. Verify
   `dotnet build hosted/ReleaseTwin.Hosted.slnx` + `cd web && npm run build`.
3. Operator: create the repo, re-point OIDC trust, move secrets/vars, reconnect
   Vercel, run bootstrap from the new repo.
4. Verify: `deploy-hosted` green from `releasetwin-platform`; Vercel builds.
5. Mirror `ReleaseTwin` → `filter-repo --invert-paths` (private set) →
   force-push `ReleaseTwin`. Reconcile root files (`.sln`, `REUSE.toml`,
   `README`, `CLAUDE.md`). Verify `dotnet test ReleaseTwin.sln` +
   `openspec validate --all --strict` + `reuse lint`.
6. Local clones re-clone / `reset --hard`.
7. `go-public-sequence` continues in the private repo; 2.4 flips the trimmed
   public `ReleaseTwin`.

## Open Questions

- `.claude/` `.cursor/` `.agents/` — keep the generic OpenSpec skills in the
  public repo, strip company specifics from `CLAUDE.md`? (Leaning yes.)
- `data-export`: doc public (anti-lock-in guarantee), spec private (hosted
  contract) — confirm.
- `demo/record.sh` fetches LD creds from AWS Secrets Manager — genericise the
  secret path for the public copy.
