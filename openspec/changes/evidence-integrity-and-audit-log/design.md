## Context

> **DRAFT.** Captures the architecture thinking so a real prospect trigger can
> turn this into queued work fast. Not validated `--strict` (spec deltas are
> sketched in the proposal's Capabilities, not yet written out).

Evidence today: captured in the engine, redacted CLI-side
(`evidence-capture` spec), uploaded via `IngestClient`, stored in S3 +
single-table metadata (`evidence-store`). Fixtures are SHA-256'd; the evidence
*bundle* is not. `plan-catalog` already advertises a per-tier "audit log"
boolean with nothing behind it.

## The two parts, and why one draft

Part A (evidence integrity) and Part B (audit log) are independent to build but
answer the same procurement conversation ("prove the artifact, show me the access
trail"). Keeping them in one draft keeps that framing visible. **When picked up,
split into two changes** — `evidence-integrity` first (smaller, on the artifact
path that is the product), `audit-log` second (larger, pure hosted).

## Key decision — manifest is CLI-produced, post-redaction, hosted-verified

The digest is computed by the CLI over the *redacted* bundle, immediately before
upload, and recomputed by the hosted store on receipt. This keeps the
trust boundary where `evidence-capture` already puts it (un-redacted evidence
never leaves the runner) and makes the hosted check a pure function of received
bytes.

**Alternative rejected:** hosted-side digest only (store hashes what it receives,
no CLI manifest). Simpler, but proves only "unchanged since upload" — not
"this is the bundle that run produced". The CLI manifest, optionally signed with
a CI OIDC identity, is what an auditor actually wants.

**Alternative rejected:** sign the un-redacted bundle in the engine before
redaction. Breaks the redaction guarantee (the signature would attest content
that must never be transmitted) and couples signing to the AGPL engine.

## Key decision — audit log on the existing single table, no mutation API

`AUDIT#<orgId>` partition, `<ISO-8601-ts>#<ulid>` sort key; a GSI on
`actor` and on `target` for the filter cases. No update/delete path is exposed —
immutability is "the API has no verb for it" plus DynamoDB PITR, not a hash
chain. **Alternative rejected:** a hash-chain / Merkle log for cryptographic
append-only proof — real work, and beyond what the expected asks
("show me who accessed this evidence") need. Note it in Open Questions so a
prospect who *does* need it re-opens the decision.

## Manual steps

None anticipated beyond the usual deploy. Signing via CI OIDC needs a documented
workflow recipe (like the `github_actions_e2e` OIDC role) but no standing console
config.
