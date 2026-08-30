# Company operations — the small-vendor credibility setup

How comparable 1–2 person open-core dev-tool companies actually run, and the concrete checklist
for ReleaseTwin. Companion to `go-to-market.md` (the "Company & billing" section) and
`self-serve-funnel-plan.md` (Workstreams A, D, F).

The thesis: for a tiny independent vendor, credibility is bought cheaply with a domain, domain
email, real TLS, a legal entity, and a Merchant of Record — **not** with fancy infrastructure.
The comparable companies all run deliberately boring stacks.

---

## How the comparable companies operate

| Company | Domain / DNS | What they actually run | Payments | Paid-artifact delivery |
|---|---|---|---|---|
| **Sidekiq** (Contributed Systems LLC — Mike Perham, 1 person) | `sidekiq.org`, own DNS | A license-admin app + a **private gem server** (`gems.contribsys.com`). No customer-facing SaaS. | Historically FastSpring (MoR), later Stripe | Access to a private RubyGems source, gated by per-customer Basic-Auth creds |
| **Oban Pro** (small US LLC — Parker & Shannon Selbert, ~2 people) | `oban.pro` / `oban.dev`, own DNS | Marketing site + a licensing/checkout app. No SaaS. | Stripe | Private **Hex organization** repo + a per-customer auth key |
| **Plausible** (Plausible Insights OÜ, Estonia via e-Residency — 2 people) | `plausible.io`, Cloudflare DNS | A real SaaS on **bare-metal Hetzner dedicated servers**: Phoenix + ClickHouse + Postgres, run by the two founders. Went bare-metal for ClickHouse cost. | Paddle (MoR) | SaaS; self-host is a Docker Compose bundle |
| **Transistor.fm** (Justin Jackson & Jon Buda, 2 people) | `transistor.fm`, own DNS | Rails on **Heroku**, audio on S3 + CDN. Deliberately boring. | Stripe | SaaS |
| **TablePlus** (small team, Singapore) | `tableplus.com`, own DNS | Native desktop app → a site, a **license-activation server**, a download/update CDN. | **Paddle** (MoR) | Signed desktop builds + license key |
| **Judoscale** (Adam McCrea, small team) | `judoscale.com`, own DNS | Started as a **Heroku Add-on** (Heroku did billing + provisioning). Now a small Rails app + a metrics ingest endpoint, direct billing added later. | Stripe (+ Heroku originally) | SaaS / platform add-on |

*(Billing-vendor details are from public writeups and may have shifted. The domain/DNS answer is
"yes, always"; the MoR-vs-Stripe split is roughly as shown.)*

### The pattern

1. **Own domain, used for everything — especially email.** `support@` / `founder@` /
   `security@` on the domain (Google Workspace or Fastmail, ~$6/user/mo). None use a public
   `gmail.com`. Highest credibility-per-dollar move there is.
2. **Own DNS**, almost always Cloudflare free tier — which is also where SPF / DKIM / DMARC
   records live, protecting deliverability.
3. **Real TLS on every hostname.** No raw cloud URLs (`*.lambda-url.*`, `*.vercel.app`,
   `*.herokuapp.com`) in front of customers.
4. **A legal entity on the invoices.** US single-member LLC is the default (Sidekiq, Oban,
   Judoscale); Plausible used Estonian e-Residency. ~$0–500 to form.
5. **Merchant of Record for payments, not raw Stripe.** Paddle / FastSpring / Lemon Squeezy /
   Polar are the *seller of record* — they collect and remit sales tax / VAT in every
   jurisdiction and issue compliant invoices. Stripe Tax only calculates; the registration and
   filing liability stays with you. For a 1–2 person company selling globally an MoR turns an
   unbounded compliance problem into a ~5–8% fee.
6. **Boring, single-region infra.** Heroku / Render / Fly / Hetzner / one big VPS. Nobody runs
   Kubernetes. Plausible's bare-metal is the *most* exotic setup in the list and it's just
   rented dedicated boxes.
7. **The elegant delivery trick** (Sidekiq, Oban): the paid product is *access to a private
   package repo* + a license key. Almost no infra. Not available to ReleaseTwin — see below.

### Where ReleaseTwin differs

Sidekiq and Oban sell **a private package**: the entire commercial footprint is a checkout page
plus a registry with per-customer auth. ReleaseTwin sells **a hosted SaaS** (dashboard, ingest
API, evidence store), which is a heavier operational commitment for a small team — uptime,
durability, on-call, a status page. That's a real choice, not an accident, and it's why the
open-core CLI matters: it's what keeps the *customer* safe when the service has a bad day (see
`continuity.md`). Plausible and Transistor prove two people can run a SaaS; just go in
clear-eyed that the model is "operate a service," not "sell a license file."

---

## ReleaseTwin checklist

| Item | Cost | Status | Notes |
|---|---|---|---|
| Register the domain | ~$12/yr | ☐ | Pick the canonical host (apex vs `www`) and redirect the other |
| Cloudflare DNS + SPF/DKIM/DMARC | free | ☐ | |
| Google Workspace / Fastmail on the domain | ~$6/mo | ☐ | `hello@`, `security@`, `founder@` |
| Replace every `ernestoalejo22@gmail.com` on the site | — | ☐ | `pricing/page.tsx`, `docs/security/page.tsx`, contact links |
| `NEXT_PUBLIC_SITE_URL` set in Vercel to the real domain | free | ☐ | Until then sitemap/robots/OG emit a fallback host |
| Custom domain + TLS on the ingest API | free (ACM) | ☐ | Currently a raw Lambda Function URL — Workstream D |
| Form a US single-member LLC | ~$50–500 | ☐ | Names the ToS, Privacy Policy, invoices |
| Choose a Merchant of Record | — | ☐ | Paddle / Lemon Squeezy / Polar — decide before Workstream F |
| ToS + Privacy Policy pages naming the entity | draft free | ☐ | Workstream D; legal review later |
| Status page | free–$20/mo | ☐ | Once there are paying customers (BetterStack, or static) |
| Compute layer | — | ✅ | Lambda + Vercel + Clerk + DynamoDB + S3 is already lower-ops than the comparables. **Add nothing.** |

---

## What not to do

- Don't add infrastructure to look serious. The comparables' credibility comes from the domain,
  the entity, and the MoR — the compute layer is invisible to customers and yours is already
  fine.
- Don't run raw Stripe globally and plan to "deal with tax later." Later is a stack of
  jurisdiction registrations. Start on an MoR.
- Don't put a personal Gmail on a security-disclosure line.
- Don't self-host email. Use Workspace/Fastmail for mailboxes and Postmark/SES for transactional
  send.
