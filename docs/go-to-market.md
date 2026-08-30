# Go-to-market

The engineering go/no-go is cleared (see README, "How far is this from commercial use?").
This doc is the commercial half: the model, the funnel, pricing, and the validation call.

Nothing here is validated. It's a starting position to be corrected by the first ten real
conversations and the first real funnel data — not a plan to execute blind.

---

## The model: open core + free self-serve funnel + SaaS tiers

Price is **never the first interaction.** A visitor can get real value without a card, without a
call, without a number on screen. The paid conversation happens later, with someone who has
already used the free product and hit a wall.

### The ladder — nobody is ever pushed to the next rung

| Rung | What they do | Cost | Friction | What has to exist |
|---|---|---|---|---|
| 0 | Read the page, get the "green ≠ proof" idea, watch a 20-sec `FLAGPROOF` clip | free | none | landing page + demo asset |
| 1 | `docker run` the example, point it at **their own** REST API, see real evidence | free | ~10 min, **no signup** | published image (done), a scaffold command, quickstart |
| 2 | Sign up, issue a token, wire the CLI into CI, watch run history land on the dashboard | free | an afternoon | public hosted platform, NuGet tool + GitHub Action |
| 3 | Hit a real limit — non-Azure-DevOps flag source, non-REST system, evidence retention, SSO, help wiring a gnarly workflow | **now** a paid conversation, and they came to *you* | — | plan-tier walls with a clear upgrade path |
| 4 | Annual plan or a setup engagement | $$ | already sold | Stripe (or manual invoicing at first) |

The person who bounced at "$8k / 6 weeks / unproven" never sees a price. They see a tool they
can run against their own API in ten minutes. Once they've done that, "unproven" stops
mattering — they proved it to themselves at zero risk. **The antidote to "unproven" is
try-before-anything, not a discount.**

### Why open core

- OSS the CLI + Core + adapters (already source-available — make it official, Apache-2.0). Free
  forever, runs entirely in the customer's infra, no account.
- It *is* the top of the funnel, and it reinforces the trust story that's already central: the
  redaction code that runs before anything uploads is code they can read.
- The hosted platform (`hosted/` + `web/`) stays commercial — source-available under a
  non-compete license (BSL-style) or kept private, decided in the funnel plan.
- It also answers the biggest objection to a two-person vendor — *"what if you disappear"* — for
  nearly free: the core is self-hostable today, and a published **continuity commitment**
  (`docs/continuity.md`, mirrored on the security page) promises hosted licenses convert to
  perpetual and the hosted source opens if the company winds down. Funded competitors structurally
  can't match this; their valuation depends on lock-in.

---

## Company & billing

- **Entity:** a US single-member LLC before public signup — it's what the ToS, Privacy Policy,
  and invoices name. Cheap to form; not a launch blocker but do it early.
- **Email on the domain** (`hello@` / `founder@` / `security@` via Google Workspace or Fastmail).
  A `gmail.com` address on a pricing page and a security-disclosure line undercuts the whole
  trust pitch. Replace every `ernestoalejo22@gmail.com` reference on the site.
- **Payments via a Merchant of Record** (Paddle, Lemon Squeezy, Polar — *not* raw Stripe). The
  MoR is the seller of record: it collects and remits sales tax / VAT in every jurisdiction and
  issues compliant invoices. Stripe Tax only *calculates* — you'd still have to register and file
  in dozens of places once thresholds trip. For a 1–2 person company selling globally, an MoR
  converts an unbounded compliance liability into a ~5–8% fee. Revisit only if that fee becomes
  material at scale.
- **Infra stays boring.** The current Lambda + Vercel + Clerk + DynamoDB + S3 stack is already
  lower-ops than the Heroku/Hetzner setups the comparable solo companies run. Add nothing.

`docs/company-ops.md` has the full checklist and how comparable 1–2 person open-core companies
(Sidekiq, Oban, Plausible, Transistor, TablePlus, Judoscale) handle hosting, DNS, and billing.

---

## Pricing benchmark — what teams already pay for adjacent tools

The point isn't to copy a competitor's number. It's to know the shape of the budget line
ReleaseTwin lands on and the buyer's reflex reaction before ROI enters the picture.

| Tool | For | Typical real spend | Budget owner |
|---|---|---|---|
| BrowserStack / Sauce Labs | Cross-browser/device test infra | $150–500/mo small; $1–4k/mo mid-market | QA / Eng platform |
| LaunchDarkly | Feature-flag management | ~$10–20/seat/mo entry; $15–50k/yr mid-market | Platform / Eng |
| TestRail / Xray / Zephyr | Test-case management + run history | $30–75/user/mo; ~$8–18k/yr for a 20-person QA org | QA management |
| Datadog CI Visibility / Buildkite Test Analytics | Flaky-test detection, CI test insight | $30–50/host/mo add-on; often $10–40k/yr mid-market | Eng platform / DevEx |
| Contract QA / release engineering | Manually assembling release evidence, regression sign-off | $8–20k/mo fractional; $120–250/hr agency | Eng leadership / release mgmt |

**What it tells us:** the monthly-SaaS instinct ($149/$499) anchors to the wrong comparables —
it reads as "another small dev tool, easy to churn." ReleaseTwin's value story is closer to the
**contract-QA / release-engineering** line: it de-risks work a human does by hand before a
release. The differentiator (flag proof + evidence-linked deterministic cases) has **no clean
competitor**, so there's no market price to anchor to — the anchor has to come from *cost
avoided*, which is exactly what the validation call surfaces. Likely buyer: **eng leadership or
release/platform**, not QA-tooling budget.

---

## The tiers (self-serve, on the pricing page)

Numbers are hypotheses. The public pricing page shows **"Early access — talk to us"** for Team
until a validated annual number exists; no monthly price appears anywhere (see the benchmark
above — a monthly figure anchors to the wrong comparables and reads as "easy to churn").

| | **Free** | **Team** | **Enterprise** |
|---|---|---|---|
| Price | $0 | Annual, from ~$3–6k/yr (set from real usage) | "Talk to us" |
| Projects | 1 | unlimited | unlimited |
| Evidence viewer | off (upgrade prompt) | on | on |
| Evidence retention | 30 days | up to 365 days | custom |
| Run history | ✓ | ✓ | ✓ |
| SSO, audit log, private deployment | — | — | ✓ |
| Support | community / GitHub | email | SLA |

`plan-tier-gating` already implements Free/Paid, the 1-project Free cap, and a self-serve
no-payment upgrade placeholder. `usage-metering` already counts uploaded reports per org per
month. The tier table above is mostly wiring those two to real limits + a real payment step.

---

## The paid engagement (rung 3, not homepage copy)

For customers who, after using the free product, ask for help wiring their real workflow —
because it genuinely needs per-target adapter/flag work. Framed as *"we'll get your first
critical workflow proven,"* never *"buy a pilot."*

- **Internal starting number: $8,000, fixed, ~6 weeks, one workflow, fee refunded if it isn't
  running against their real workflow at the end.** Below the ~$10k procurement-committee line;
  the refund clause removes the "will it work with our stack" objection.
- **First 1–2 of these: ~$2k, never free**, explicitly as founding design partners, in exchange
  for a named case study and deep feedback. A token fee still filters tire-kickers and produces a
  real willingness-to-pay signal; a free pilot gets deprioritized inside the customer's org and
  proves nothing. You have zero references, and $2k + a case study is a fair trade for both sides.
- **Founding-customer pricing:** the first ~10 accounts that convert to an annual plan lock their
  rate for 3 years. Rewards early belief, creates urgency, and does it without discounting the
  list price for everyone.
- Post-engagement: annual Team/Enterprise plan. Provisional $12–30k/yr for Enterprise-shaped
  accounts; say this only if asked, and only as "we'll have real numbers from your own run."
- **Retire the $149/$499/mo tiers from all external conversation.**

---

## Landing-page sketch

- **Hero:** the problem + the idea, one line each. *"Green in staging isn't proof your fix
  works. ReleaseTwin runs the same case known-bad and known-good and tells you which build is
  actually fixed."*
- **Demo:** a terminal recording (asciinema) of `docker run … → FLAGPROOF FLAGPROOF-DEMO-1
  (Passed)`. The `demo/naha-releasetwin-flow.mp4` asset is a starting point.
- **Primary CTA:** **"Try it — no signup"** with the copy-paste `docker run` command inline.
- **Trust section:** "Execution and your data stay in your infra. Only hashes and pass/fail
  leave — and even that's optional. Redaction runs in your CI, in code you can read."
- **Secondary CTA:** "Create a free account" → dashboard.
- **Design-partner note (an asset, not an apology):** *"We're working hands-on with a small
  number of design partners — free, in exchange for feedback."*
- **Pricing page:** the three tiers above. Nothing over ~$100/mo. Enterprise = a contact form.

The current `web/src/app/page.tsx` already has a hero, a live dashboard preview, and a
feature grid — it needs the no-signup `docker run` CTA, the trust section, and a real pricing
page, not a rebuild.

---

## The validation call — script

Run this **before** offering any engagement, ideally as a separate 30-minute call. The goal is
to learn whether the pain is real and budgeted, not to sell. Weak answers → don't pitch, you
just saved six weeks.

### Open (2 min)

> "I'm not going to demo anything today. I want to understand how your team proves a release
> candidate is actually safe — especially with integrations and feature flags involved — and
> where that hurts. Fifteen minutes of questions, then I'll tell you honestly whether what
> we're building is relevant."

### The six questions (concrete pain → budget)

1. **"Think about your last bad release or near-miss — something broke that testing should have
   caught, or you weren't sure a fix actually fixed it. Walk me through it. What did it cost —
   engineer-hours, customer impact, a rollback, a war room?"**
   *The anchor for all ROI math. You want a number or a story with a number in it. Can't produce
   one → the pain isn't acute; yellow flag.*

2. **"When you cut a release candidate today, what evidence gets assembled that it's safe — and
   how much is someone doing by hand?"**
   *Listen for: manual regression sign-off, screenshots pasted into a ticket, someone "checking
   the flags," a release checklist nobody trusts. That's what ReleaseTwin replaces.*

3. **"How many workflows are genuinely release-critical — a regression there is a real incident —
   and how many release candidates go through them per month?"**
   *Sizing. 3 workflows × 8 releases/mo is a very different product than 30 × 40.*

4. **"When you ship a fix behind a flag, how do you prove the fix works with the flag on AND that
   the old broken behavior is really gone with it off? Or do you?"**
   *The flag-proof wedge. Most teams have no clean answer. The length of the pause tells you how
   hard the differentiator lands.*

5. **"Does execution and test data have to stay inside your network? Any compliance or audit
   requirement on release evidence — SOC 2, HIPAA, an internal control?"**
   *Answerable in our favor. A "yes, must stay internal" is a buying signal for us, not an
   objection.*

6. **"If this were solved well, whose budget pays — engineering, QA, platform, release
   management, compliance? Who signs off on a $5–15k tool purchase there?"**
   *The most important question. Enthusiasm from someone with no budget is a dead end. You want
   a name and a number.*

### Close (2 min)

> "Based on what you've said — [reflect back the incident cost, the manual work, the flag
> situation]. Here's what we'd do: get that one workflow wired and proven, ~6 weeks, fixed fee,
> refunded if it's not running at the end. Want the one-pager?"

If Q1 and Q6 were both strong, pitch. Otherwise: "I don't think we're the right fit for you
yet — here's what would have to change," and move on. A clean no beats a soft maybe.

### Signal grading

- **Green:** named incident with a cost, existing manual evidence work, named budget owner, no
  clean flag-proof answer. → pitch.
- **Yellow:** pain real but diffuse, no incident cost, budget owner unclear. → stay in touch.
- **Red:** "interesting, send info," no incident, enthusiasm but no budget. → disqualify, don't
  chase.

Enthusiasm for the demo mechanics is **not** willingness to pay. The only signals that count are
a funded engagement and a specific budget owner who returns your email.

---

## What to build to support this

See `docs/self-serve-funnel-plan.md` — the engineering workstreams (OSS licensing, get-started
friction, distribution surfaces, making the platform public, billing, conversion walls), their
dependencies, and the minimum set to launch the funnel without building Stripe first.

## Tracking

Keep a spreadsheet (not this repo) of every conversation: company, date, incident cost cited,
manual work described, flag-proof answer, budget owner, green/yellow/red, next step. After five
calls, revisit the model, tiers, and benchmark with what you actually heard.
