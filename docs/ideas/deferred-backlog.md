# Deferred feature backlog

**Status:** parked. Compiled 2026-09-01 from README "What's not built yet", the
funnel plan, and `pre-pilot-tracks`. None of these are queued work and none are
on the path to a first paid design-partner pilot. They live here — not in
`openspec/changes/` — so they don't read as scheduled.

**Rule:** do not write a proposal for any of these speculatively. Reach for one
only when a real prospect or pilot names it. The ranking below is a guess at
*which a pilot is most likely to ask for*, nothing more.

---

## Likely a pilot triggers

### 1. CLI packaging — Homebrew tap, single-file per-RID binary
**Mostly resolved by `cli-distribution` (2026-09-01):** the CLI now ships as a
Docker image, a `dotnet tool` on nuget.org, and a GitHub Action. What's left is
a Homebrew formula and a self-contained single-file binary (no .NET runtime).
Low urgency — the three shipped paths cover the demand. Source: funnel plan
Workstream C.

### 2. Project-level `control` template for flag proof — ✅ DONE (2026-09-02)
Shipped as `flag-proof-project-template` (archived
`2026-09-02-flag-proof-project-template`): a `releasetwin.yml` at the cases-dir
root carries a shared `flag_proof.control`; a case's inline block deep-merges
over it.

### 3. Flag proof against non-REST / SDK-only flag stores
The HTTP `control` block only covers REST-toggleable systems. An SDK-only or
streaming provider needs a new `IFeatureStateController`. Source: README,
`flag-proof-http-control`.

---

## Maybe

### 4. Deeper PR integration — status check + evidence link back to the PR — ✅ mostly DONE (2026-09-02)
Shipped as `pr-annotation-evidence-link` (archived
`2026-09-02-pr-annotation-evidence-link`): the Action already rendered a check
run; the ingest response now returns dashboard URLs and the annotation gains a
"View run" link + per-case evidence links (when a hosted token is set). A legacy
commit-status (Statuses API) was considered and rejected — the check run already
gates a protected branch. Hosted PR-history is still separately deferred and
Team-gated.

### 5. External-check connector (Playwright) — visual / browser evidence
Not wired into the check pipeline at all. Related but distinct from the parked
`visual-flake-classification` idea. Source: README.

### 6. SSO / SAML
`enterprise-access` covers VPN / no-inbound / Entra OAuth for the flag-proof
`control` leg, not dashboard SSO. An enterprise procurement ask. Source:
`pre-pilot-tracks`.

### 7. Audit log + signed / tamper-evident evidence
Enterprise / compliance asks. Evidence bundles are currently trusted-by-storage,
not cryptographically signed. Source: `pre-pilot-tracks`.

---

## Unlikely pre-pilot

### 8. Non-REST adapter (message queue / database / vendor SDK)
The HTTP adapter covers any REST surface. Anything without one still needs
bespoke adapter code. Deliberately deferred — the core/adapter boundary is
proven; this is volume, not risk. Source: README.

### 9. Azure DevOps data-driven operations
AzDO operations are still fixed-shape; only the HTTP adapter is data-driven from
case files. Source: README.

### 10. Hosted fixture store
Deferred. Source: `pre-pilot-tracks`.

### 11. Status page + SLA (status subdomain, uptime monitor, incident history)
Marketing copy was scrubbed to stop referencing it (`pre-pilot-missing-features`);
`company-and-domain-launch` lists it as explicitly deferred. Needed before an
SLA can be offered, not before a pilot. Source: `pre-pilot-missing-features`,
`company-and-domain-launch`.

### 12. `/blog` + ongoing content / SEO
`robots.ts` / `sitemap.ts` / OG images are built; there is no content channel.
A slow second-order acquisition channel. Source: GTM notes.

---

## Not features — tracked elsewhere

- `run-notifications`, `evidence-sharing` — built, feature-flagged **OFF**.
  Decide per-flag when a pilot uses them.
- Polar reconciliation / upgrade flow out of dry-run — tracked in
  `company-and-domain-launch` §7.
- Prod-stack decision, repo-public flip, self-serve sign-up exposure — tracked
  in `go-public-sequence`.
