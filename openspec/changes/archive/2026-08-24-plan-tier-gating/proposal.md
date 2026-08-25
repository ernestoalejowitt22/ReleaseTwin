## Why

`usage-metering` gave the hosted platform a way to observe usage; nothing yet acts on it. Per explicit decision, the first real entitlement gate is a free-tier project-count limit (1 project on Free, unlimited on Paid), set via a self-serve "Upgrade" button rather than real payment — there is still no Stripe integration (Stage 2 of `hosted-self-serve-platform` remains deliberately deferred), so this is a placeholder for the eventual paid flow, not billing itself. This is scoped deliberately narrow: a project-count cap only affects project *creation* (low-frequency, non-disruptive), not the ingest hot path — a usage-volume cap would force a decision about blocking uploads mid-month that contradicts the existing "upload failure is a warning, never blocks a case" principle, and was rejected for that reason.

## What Changes

- Add a `PlanTier` (Free/Paid) field to `Organization`, defaulting every organization to Free at creation.
- Enforce a limit of 1 project for Free-tier organizations; Paid organizations are unlimited. Attempting to create a second project on Free is rejected with a clear reason, not a silent failure.
- Add a self-serve "Upgrade" action: no payment collection, just flips the organization's `PlanTier` to Paid. A placeholder for the real paid flow, not itself billing.
- Surface the current tier and an "Upgrade" control in the dashboard.

## Capabilities

### New Capabilities
- `plan-tier-gating`: an organization's plan tier, the project-count limit it enforces on Free, and the self-serve (no-payment) upgrade action that lifts it.

### Modified Capabilities
- `dashboard`: shows the organization's current plan tier and, when on Free, an "Upgrade" control.

## Impact

- `hosted/ReleaseTwin.Hosted.Api/Data/Entities/Organization.cs`: new `PlanTier` field.
- `hosted/ReleaseTwin.Hosted.Api/Services/ProvisioningService.cs`: `CreateProjectAsync` gains a tier check before creating a project.
- New: an upgrade endpoint/service action flipping `PlanTier` for the caller's own organization.
- `web/src/app/dashboard/page.tsx`: render tier + Upgrade control; a new server action for the upgrade call.
- No changes to `ReleaseTwin.Core`, the CLI, ingest behavior, or usage-metering's counting logic — this change only gates project creation, nothing upload-related.
- No Stripe/payment integration — explicitly out of scope, per Why.
