## Why

> **DRAFT — not queued.** Per `docs/ideas/deferred-backlog.md` the rule is: no
> proposal for a deferred item until a real prospect names it. This is written up
> so the shape is ready the moment an enterprise security/procurement review asks
> for it. Do not start `openspec apply` on this change without that trigger.

Two enterprise/compliance asks share a root cause — the platform records *what*
happened but gives no way to prove the record was not altered, and no history of
*who looked*:

1. **Evidence is trusted-by-storage.** A captured evidence bundle is redacted on
   the runner, uploaded, and stored. Nothing binds the bundle to the run that
   produced it or detects a later edit. A customer whose auditor asks "how do you
   know this screenshot is the one from that run?" has no answer.
2. **No audit log.** `plan-catalog` spec text already says a tier advertises
   "whether the audit log is available" — but no audit-log capability, storage,
   or query surface exists. Org/project/membership/entitlement changes and
   evidence-access events are not recorded anywhere queryable.

## What Changes

**Part A — tamper-evident evidence**

- The CLI emits an **evidence manifest** alongside each uploaded bundle: a hash
  (SHA-256) over every artifact in the bundle (evidence document, each
  screenshot, each step record) plus the run's case id, fixture hash, and build
  identity — a single content digest for the whole bundle.
- The manifest MAY carry a **detached signature** when the runner is given a
  signing key (`${RELEASETWIN_SIGNING_KEY}` or a keyless OIDC path in CI);
  unsigned manifests stay valid — signing is opt-in.
- The hosted store **verifies the manifest on ingest** (recomputes the digest
  over what it received) and records the result; a mismatch is stored as a
  distinct non-fatal signal, like the existing "evidence not accepted" path.
- The dashboard shows an **integrity badge** per bundle: unverified / digest-ok /
  signature-ok (signer identity).

**Part B — audit log**

- A new hosted **append-only audit log** capability: every
  org/project/membership/role/entitlement mutation and every evidence-access
  event (view, share-link create/revoke, download) writes an immutable entry —
  actor, action, target, timestamp, request context.
- **Query surface**: an operator/admin dashboard view and an export endpoint,
  org-scoped, filterable by actor/action/target/date. Entries are never editable
  or deletable through any API.
- **Tier-gated** via the entitlement service, matching the `plan-catalog`
  "audit log available" flag — Enterprise-only, closed by default.
- Retention: a system default with a tier ceiling, mirroring the evidence
  retention model.

## Capabilities

### New Capabilities

- `evidence-integrity`: the evidence manifest (digest + optional detached
  signature), its emission by the CLI, its verification on ingest, and the
  integrity signal surfaced to the dashboard.
- `audit-log`: the append-only hosted audit log — what events are recorded, the
  immutability guarantee, the org-scoped query/export surface, and tier gating.

### Modified Capabilities

- `evidence-capture`: the CLI additionally produces a manifest over the redacted
  bundle before upload (redaction still runs first and unchanged).
- `evidence-store`: stored evidence carries its verification result; a
  digest/signature mismatch is a distinct non-fatal signal.
- `ingest-api`: the accepted payload MAY carry an evidence manifest; the response
  reports the verification outcome. The "no sensitive content" guarantee is
  unchanged (a digest is not content).
- `plan-tier-gating`: the "audit log" entitlement becomes a real gate rather than
  advertised-only.

## Impact

- **Engine / AdapterSdk** — evidence document gains nothing; the manifest is
  produced by the CLI post-redaction (`src/ReleaseTwin.Cli/Upload/` +
  evidence-capture path). AGPL side.
- **CLI** — new `--signing-key` / env path; manifest writer; the upload carries
  the manifest part.
- **hosted/** — ingest verification, a new audit-log table (single-table:
  overloaded PK/SK — `AUDIT#<org>` / `<ts>#<ulid>`), a query resolver, an export
  endpoint, dashboard views. BSL side. Terraform: no new infra beyond table
  items (the single table already exists); possibly a GSI for actor/target
  filtering.
- **web/** — integrity badge component; audit-log dashboard page (Enterprise).
- **Sizing**: Part A ≈ one focused change on its own; Part B ≈ a second, larger
  one. Recommend splitting into `evidence-integrity` and `audit-log` changes when
  this is actually picked up — this draft covers both so the relationship is
  visible.

## Open Questions

- Signing: keyless (Sigstore/Fulcio-style OIDC) vs. a customer-supplied key vs.
  both. Keyless is the better CI story but adds a dependency.
- Does Part A's digest need to be in the run summary / PR annotation too
  (overlaps with `pr-annotation-evidence-link`)?
- Audit-log entries for *read* events at scale — cost of writing an item per
  evidence view; sampling vs. full fidelity.
- Is a Merkle/hash-chain over audit entries (each entry hashes the prior) worth
  it for true append-only proof, or is write-once storage + no mutation API
  enough for the asks we expect?
