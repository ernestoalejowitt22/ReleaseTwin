## Context

See proposal.md for why. Relevant existing facts:
- `adapter-credentials` already solved hosted, encrypted, per-project secret storage end to end
  (entity, Data Protection encryption, SSM-persisted key ring, ClerkJwt dashboard endpoints,
  ApiToken-authenticated CLI fetch, env-var-precedence CLI integration) — this change reuses that
  entire mechanism, generalized from a fixed two-adapter field manifest to arbitrary customer-chosen
  names. No new encryption primitive, no new IAM grant: the SSM/KMS permissions the Lambda's IAM role
  already has for Data Protection key-ring persistence (`hosted-platform-deployment`) cover any
  additional `IDataProtector` purpose string transparently — they're scoped to the key-ring parameter
  path, not to a specific protector.
- `CliRunner.cs` currently resolves `${VAR_NAME}` references indirectly: it receives its own
  `environment` dictionary (a snapshot Program.cs takes via `Environment.GetEnvironmentVariables()`,
  used for CliRunner's own adapter/token logic and already the seam `CliRunnerTests` inject fake
  values through), but `CaseFileLoader`'s `${VAR_NAME}` interpolation (case-loading spec) reads the
  live process environment directly via `Environment.GetEnvironmentVariable(varName)` — a second,
  separate read path, not sourced from CliRunner's own dictionary today. This gap matters for this
  change: a hosted-fetched secret has to reach the same resolution point `${VAR_NAME}` already uses,
  and today that point isn't injectable.
- `plan-tier-gating` already has a `PlanTier` enum (Free/Paid) on the organization and an existing
  enforcement precedent (`free-tier-project-limit`, checked server-side, surfaced to the dashboard as
  a distinct error code rather than a generic failure) — this change's Paid-tier gate follows the
  same shape.

## Goals / Non-Goals

**Goals:**
- A customer can set, rotate, and revoke an arbitrary-named project secret through the dashboard.
- `${VAR_NAME}` in a case file resolves from a hosted-stored secret when the local environment
  doesn't have it, with zero behavior change for a customer who never adopts this.
- Reuse 100% of `adapter-credentials`' encryption and key-management machinery — this is a shape
  change (fixed manifest → arbitrary name) and a new fetch/precedence integration point, not a new
  trust or infrastructure story.

**Non-Goals:**
- Not a general-purpose secrets manager for anything outside a journey/case's own `${VAR_NAME}`
  references (see proposal.md).
- Not replacing `adapter-credentials`'s two structured vendor forms — those keep their dedicated
  fields; this is additive for everything else.
- Not adding hosted execution — resolution happens inside the CLI process, exactly like
  `adapter-credentials` (see proposal.md's load-bearing Non-Goal).
- Not building bulk import/export of secrets, or secret versioning/history beyond "rotate replaces
  the value entirely" — matches `adapter-credentials`' own scope.

## Decisions

**One entity, `ProjectSecret`, keyed by (ProjectId, SecretName) — one item per secret, not one item
per project holding a name→value map.** Unlike `AdapterCredential` (a small, fixed field set always
set/fetched together), a project can accumulate an arbitrary, growing number of independently-added
secrets; per-secret items let the dashboard add/rotate/revoke one secret without a read-modify-write
race against every other secret on the same project, and let the list endpoint page/enumerate
naturally. Same single-table DynamoDB shape as every other entity here (new key prefix under the
project's partition, no new GSI).

**Encrypt with a distinct Data Protection purpose string (`"ProjectSecrets.v1"`), isolated from
`AdapterCredentials.v1` and `ConnectionStateService`'s own protector**, same isolation rationale
`adapter-credentials` already established — different purpose strings can never cross-decrypt each
other's ciphertext, so a bug in one can't expose the other. Same key ring, same SSM persistence,
already covered by existing infrastructure (see Context).

**The CLI-facing fetch endpoint returns the project's full secret set in one call
(`GET /api/cli/project-secrets`, scoped by the token's `project_id` claim, same structural
wrong-project-denial shape `adapter-credentials` uses — no separate resource ID to probe), not a
per-name lookup.** `CliRunner` doesn't know in advance which `${VAR_NAME}` names a case file will
reference before parsing it, and case files can reference many different names — fetching once per
run and resolving every reference against that set avoids N round trips and avoids restructuring
case-loading into a two-pass "scan for references, then fetch, then parse" pipeline. Trade-off:
every run with a configured project token does one fetch even if no reference needs it, mirroring
the same trade-off `adapter-credentials` already accepts for install-time adapter resolution.

**Close the environment-injection gap by giving `CaseFileLoader` an injectable environment lookup
(defaulting to today's live `Environment.GetEnvironmentVariable` behavior when not supplied),
instead of mutating real process environment variables.** Considered two approaches:
- *Mutate the process*: `CliRunner` calls `Environment.SetEnvironmentVariable` for each fetched
  secret name not already set, before invoking the loader — zero change to `case-loading` code, but
  pollutes real process state for the rest of the CLI invocation (visible to any child process, e.g.
  the UI adapter's browser subprocess) and breaks the injectable-dictionary testability convention
  `CliRunner` itself already follows for its own environment parameter.
- **Chosen: thread a lookup through.** `CaseFileLoader` gains an optional environment-resolution
  seam; `CliRunner` builds one effective lookup (local environment first, hosted-fetched secrets as
  fallback) and passes it through to the loader it already constructs. Slightly larger code change,
  but consistent with how this codebase already treats environment as an explicit, testable input
  everywhere else, and avoids any real-process-state side effect.

**Paid-tier gate enforced at the dashboard write endpoint (set/rotate), not at the CLI fetch.** A
customer who was Paid when they set a secret and later downgrades shouldn't have already-running CI
break on the next fetch — the gate controls creating new commitments, not honoring ones already
made. Mirrors the spirit of `plan-tier-gating`'s existing project-limit check being a write-time
gate, not a read-time one.

## Risks / Trade-offs

- **A growing, unbounded number of per-project secret items** (no cap stated in the spec). →
  Accepted for this pass; a future limit (if abuse or cost becomes real) is a `plan-tier-gating`
  concern layered on top, not a reason to block this change.
- **The full-secret-set-per-fetch shape means a case file with a typo'd `${VAR_NAME}` still triggers
  a real network call before failing.** → Accepted: matches today's `adapter-credentials` fetch
  behavior for an unconfigured adapter, and the failure is still a clear, immediate load-time error
  either way.
- **Threading an injectable environment lookup through `CaseFileLoader` touches existing,
  well-tested code.** → Mitigated by defaulting the new seam to today's exact live-environment
  behavior when the caller doesn't supply one, so every existing call site (and its tests) is
  unaffected unless it opts in.

## Migration Plan

No data migration — new entity, new endpoints, nothing existing changes shape. No new deployment
step: this reuses the SSM-persisted Data Protection key ring and IAM permissions
`hosted-adapter-credentials` already required and `hosted-platform-deployment` already granted in
production.
