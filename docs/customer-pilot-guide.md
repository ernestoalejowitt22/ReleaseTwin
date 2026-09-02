# Customer usage and pilot guide

Grounds "how would customers use it" in what's literally built today, not the aspirational version. An honest account of what is built versus aspirational — so nobody oversells real release-proof coverage as a five-minute setup.

## First-pilot readiness (assessment 2026-09-02)

A "first pilot" here means **one design partner, invited privately, running against
their own systems, on the `releasetwin-dev-` stack, free**. Against that bar — not
against GA — here is what actually stands between now and starting.

Most open tasks in `company-and-domain-launch`, `go-public-sequence`,
`cli-distribution`, and `support-intake` are for **GA / going public / billing /
scale** and do **not** block a pilot.

### Hard blockers (≈1 day of operator time + one lawyer engagement)

1. **Operator account + `ADMIN_OPERATOR_USER_IDS`.** Sign up once on the
   *production* Clerk instance, then open **`/dashboard/me`** while signed in — it
   returns `{ clerkUserId, email, isOperator, ... }`. Set the
   `ADMIN_OPERATOR_USER_IDS` repo var to that `clerkUserId`, redeploy, reload
   `/dashboard/me`, confirm `isOperator: true`. Without an operator account you
   can't set up the partner's org or grant a tier.
   (go-public-sequence §4.6; company-and-domain-launch §5.1b leftover.)
2. **Prove the Clerk `email` claim end to end.** `security-hardening-pre-pilot`
   made invitation *acceptance* require a provider-verified `email` claim — if the
   Clerk session-token customization put `email` where the API doesn't read it, or
   it isn't the verified primary, invites fail silently. Fast check:
   **`/dashboard/me`** shows the `email` it resolved — if that's your verified
   address, the claim is landing. Then do one real invite → accept round trip to
   be sure. (company-and-domain-launch 4.10; go-public-sequence 4.2.)
3. **A pilot agreement a real counterparty can sign.** Drafts are in
   [`docs/legal/`](legal/) (pilot agreement + DPA, built from the Common
   Paper / Bonterms structure). A Mexican technology lawyer needs 1–2 hours on the
   pilot agreement + the ToS governing-law/liability clause (company-and-domain-launch
   §6.7). Highest-value item on the list; skippable only for a genuinely informal
   design partner.

### Strongly recommended before sending it to a real company

4. **`@releasetwin.com` mailbox** (Google Workspace or equivalent). Not strictly
   required — a pilot can run with the personal inbox — but it's the notice
   address on the pilot agreement, the `SECURITY.md` contact, and how procurement
   reads you. Also unblocks company-and-domain-launch §6.5. ~30 min.
5. **Talk to the prospect first.** The validation questions at the bottom of this
   doc have never been asked of a real prospect. Budget commitment to build out
   *their* workflow is the signal — not demo enthusiasm.

### Explicitly NOT needed for a pilot

- **Going public** (`go-public-sequence`) — a pilot is private; the change says so.
- **Polar production / billing** (company-and-domain-launch §7) — pilot is free.
- **Incorporation** (S.A.S. / S. de R.L.) — deferred track; a registered persona
  física is a sufficient legal counterparty and Merchant-of-Record payee.
- **nuget.org / a `v0.2.0` release** (`cli-distribution`) — the documented pilot
  setup is "clone the repo, `dotnet build`" (or hand the partner the GHCR image).
- **Trademark glance, Google Search Console, invite-email deliverability, the
  `docs/company-setup.md` close-out** — GA housekeeping. The invite workaround
  (admin copies the accept link from the UI) is fine for pilot #1.

### The product reality (this is the actual work — not a checkbox)

- The **demo is real today** for anything REST-shaped: prerequisite ownership,
  cleanup, failure classification, flag proof. A live demo against a sandbox org
  is honest.
- **"It works with your system" is not true yet** for anyone but Azure DevOps in a
  fixed operation shape. Testing the partner's *actual* workflow is scoped
  engineering done *together, during the pilot* — that is what the pilot is.
- **Flag proof — the differentiator — is still Azure-DevOps-specific.** A partner
  whose flags live in LaunchDarkly / a config service / their own REST endpoint
  needs an `IFeatureStateController` built for it. Good pilot scope, not a blocker,
  but go in knowing it.
- **No pricing exists.** Don't imply it.

So a first pilot is: find a design partner whose critical workflow is
REST-reachable → sign a lawyer-glanced pilot agreement → build their Tier-2
integration together → learn whether they'd commit budget. Items 1–3 are the
gate; the rest is polish or comes later.

## Update (commercial-readiness-gaps): teams, notifications, and shareable evidence

The hosted platform is no longer single-user. A pilot can now bring their team:

- **Membership + roles.** An org owner invites teammates by email; each membership is `admin`, `member`, or `viewer`. Admins manage billing, tokens, members, and notifications; members trigger runs and view evidence; viewers are read-only (useful for a client PM or a compliance reviewer who should *see* the evidence but not push buttons). A user can belong to several orgs and switch between them in the header. Per-project pricing is unchanged — **team size is not a billing axis**, only project count is.
- **Run-failure notifications** (Team tier, opt-in, per project). A Slack incoming-webhook or a generic HTTPS webhook fires when a run fails or a flag proof doesn't discriminate. The payload carries the project, the case/run id, the result and classification, and a dashboard link — never fixture content, response bodies, or secrets. The customer-supplied URL is validated (https-only, no private/loopback/metadata addresses) at save time and again at send time.
- **Shareable evidence links** (Team tier). An admin creates a per-run, revocable, expiring link. Opening it renders exactly the redacted evidence document that run already uploaded — no dashboard, no other runs, no account surface — to someone with no login. Good for handing a proof to an auditor or a manager; the redaction still happened in the customer's own CLI before upload.

Two honest caveats for a pilot pitch: (1) both notifications and share links sit behind a master feature flag that is **off by default** — flip it on for a design partner deliberately; (2) the transactional-email sender (`SesInvitationEmailSender`) is now in the codebase but **dormant** until the `DOMAIN_NAME` + `NOTIFICATIONS_FROM_ADDRESS` repo vars are set and SES is verified — until then an invitation's accept link is returned in the API/UI for the admin to share directly rather than being emailed (a fine pilot workaround).

## Update (hosted-self-serve-platform): self-serve onboarding now exists — with real limits

A customer no longer needs "clone this repo and read the README" as the only path in. They can now:

1. Sign in (self-serve, no approval step, no requirement to already have an account on a platform unrelated to ReleaseTwin's own adapters) at the hosted control plane.
2. Create a project and issue an API token themselves.
3. Install the CLI locally/in CI, set `RELEASETWIN_API_TOKEN` (and `RELEASETWIN_API_URL`), and run it.
4. See their own uploaded run history and flag-proof results on a dashboard — scoped strictly to their own organization.

**Real, honest limits on this, stated plainly:**
- This is **Stage 1, free-only** — there is no billing, no paid tier, no usage limits enforced yet. Don't imply pricing exists.
- Execution still happens entirely in the customer's own infra. The hosted platform is a control plane (accounts, tokens, dashboard), **not** a hosted test runner — nothing about "no install" changed; what changed is that results now have somewhere to land besides a terminal.
- Only report metadata is ever uploaded (case ID, oracle reference, fixture hash, pass/fail, classification) — never fixture content, response bodies, or credentials. This is worth stating to a security-conscious prospect as a real trust property, not just reassurance.
- The hosted platform is deployed (Lambda API + Vercel frontend, auto-deploying from `main`) and a production Clerk instance is wired to it. Sign-up works — but it isn't linked or announced anywhere and no outside user has been invited. Offering it to a prospect is now a decision to make, not a setup step to finish.

## Update (phase4-generic-http-adapter): the Tier 1/Tier 2 gap is partially closed

The original version of this doc identified a real gap: every operation was hardcoded, so no prospect could test their own workflow, only a fixed Azure DevOps demo shape. That's no longer entirely true.

**Any REST API is now testable from a case file alone**, no new adapter code required — proven end to end against a live public API (`examples/cases/example-http.yaml`, a real HTTP call and real JSONPath assertions against `jsonplaceholder.typicode.com`, not a fake handler). A prospect whose release risk lives behind a REST API (their own claims service, a payments sandbox, an internal microservice) can author a case today using `http.request` + `http.assertJsonPath`, supplying method/URL/headers/body/assertions as case-file data, with credentials resolved via `${ENV_VAR}` interpolation so nothing sensitive is committed.

**What's still fixed-shape**: the Azure DevOps adapter's own operations (`azdo.createWorkItem`, etc.) remain hardcoded — a prospect whose workflow specifically needs Azure DevOps work-item behavior different from what's built would still need a new adapter or an extension to that one. The generic HTTP adapter is the one that's now parameterized.

## The honest gap, updated

```
Tier 1 — Works today                    Tier 2 — What's still missing
─────────────────────                   ──────────────────────────────
Any REST API: author a case             Non-REST systems (a message
with method/URL/headers/body/           queue, a database, a vendor
JSONPath assertions — no new            SDK with no REST surface) —
adapter code needed                     needs a new adapter, same
                                         pattern as Azure DevOps's
Azure DevOps: run the fixed
demo shape (work items,                 Azure DevOps behavior beyond
prerequisites, cleanup,                 the fixed operations already
flag proof) against your                built (custom fields, other
own org                                 work item types, etc.)
```

**Today, a prospect with a REST API can write a real case testing real business behavior, not just watch a demo.** This is a materially stronger pilot pitch than before: "bring your API, write a case, get real release-proof evidence" is now literally true for anything REST-shaped.

## Realistic customer journey today

1. **Prerequisite**: a REST API to test (any auth style expressible as headers/body) — no Azure DevOps account needed unless they specifically want that adapter too.
2. **Setup** (~15-30 min, matches the "smoke check" tier from docs/installation-model.md, though untested against a real outside user yet):
   - Clone this repo, `dotnet build`.
   - No credentials required at all for HTTP-only cases — the CLI installs the HTTP adapter unconditionally and Azure DevOps only if its 5 env vars are present.
   - Run the bundled `example-http.yaml` example, then write one against their own endpoint.
3. **What they see**: a case with a real HTTP call and a real JSONPath assertion, evidence and classification exactly as before. If they connect the hosted platform and opt into evidence capture (Paid tier), the dashboard also shows a per-run drill-down — every step's request/response summary and assertion path/expected/observed — with a talking point that lands well: **the redaction runs in *their* CLI, in code they can read, before anything is uploaded**; the hosted side never sees raw content and the ingest contract still has no field for a credential. It reframes "we upload your test data" (a non-starter for most integration-heavy teams) into "you decide what evidence is safe, we just render it."
4. **The flag-proof demo** (still the actual differentiator, still Azure-DevOps-specific for now): a case declaring `flag_proof: { feature_key, build_identity }` now runs known-bad → known-good through the CLI itself (`cli-flag-proof-runner`), printing `FLAGPROOF <id> (Passed)` when the oracle correctly discriminates — not just a manual toggle-and-rerun. Worth pursuing as a generic HTTP-based flag-proof pattern in a future change if a prospect's own workflow has a comparable toggle reachable over REST.
5. **The UI-journey demo** (`ui-journey-visual-evidence`): a journey can drive a real browser as one leg — `ui.navigate` / `fill` / `click` / `assertVisible`, plus `ui.setCookie` to get past a cookie-gated app (an E2E auth bypass, a locale, a feature toggle) — then bridge a UI-observed value into an API leg in the same case. With evidence capture on, every UI step's screenshot is redacted in the customer's CLI (auth headers, password-field values, and their own masks) and rendered on the dashboard as **visual evidence** next to the request/response detail. The pitch: "your release proof isn't just a green check — it's the screenshots and the payloads, and you decided what's safe to show." (Opt-in with `RELEASETWIN_UI_ENABLED=1`; needs Chromium on whatever runs the CLI.)
6. **What they still can't do**: test something that isn't reachable over HTTP or a browser, get Azure-DevOps-specific behavior beyond the fixed operations already built, or store fixtures in the hosted platform (they're still resolved locally). Be upfront about this before they ask.

## What's still worth building only once a prospect needs it

- **A generic flag-proof pattern for HTTP** — `FlagProofRunner` is now CLI-runnable (`cli-flag-proof-runner`), but still only has an `IFeatureStateController` implementation for Azure DevOps variable groups. A prospect whose feature flags live in LaunchDarkly, a config service, or their own REST endpoint would need a comparable HTTP-based implementation.
- **A non-REST adapter** — message queues, databases, vendor SDKs without a REST surface. Same pattern as Azure DevOps's adapter, built when a specific need names the target.

Either way: this is real, scoped engineering work that should be shaped by what the specific design partner's workflow actually requires, not guessed at in advance.

## What to say in a pilot pitch (and what not to)

**Say:**
- "The core mechanics — prerequisite ownership, cleanup, failure classification, and flag proof — are real and working today. Here's a live demo against a sandbox org."
- "Testing your actual workflow is a scoped build-out, done together as part of the pilot" — a scoped engagement, one critical workflow, assisted setup.

**Don't say:**
- "It already works with your system" — it doesn't yet, for anyone but Azure DevOps, and only in the fixed-operation shape.
- Anything implying a five-minute setup for real release-proof coverage — 1-2 engineering days is the honest target once fixtures, vendors, and flags are real.
- "There's a hosted dashboard" without the caveat that it's Stage 1, free-only, and execution is still entirely local/CI-side — don't let "hosted control plane exists" get heard as "you don't need to run anything yourself."
- Anything about pricing — none exists yet (Stage 2, not built).

## Validation questions to actually ask a prospect

These are the right questions to ask, and none have been asked of a real prospect yet. The full call script lives in the private planning notes:

1. What release or incident cost would this proof have avoided?
2. Which reports or evidence are currently assembled manually?
3. How many critical workflows and release candidates run each month?
4. Must execution and artifacts stay inside their network? (Answerable today: execution and artifacts (fixtures, response bodies) always stay inside their environment; only report metadata — hashes and pass/fail, never content — goes to the hosted dashboard, and even that's optional.)
5. Who owns the budget — engineering, QA, platform, compliance, release management?
6. Would they pay more for managed execution, governance, or private deployment?

Enthusiasm for the demo mechanics is not evidence of willingness to pay. The signal that matters is whether they'd commit budget to Tier 2 work on their actual workflow.
