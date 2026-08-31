# Continuity commitment

ReleaseTwin is built and run by a very small independent team. This document states what happens
to your ability to work if we slow down, get acquired, or shut down entirely. It is a deliberate
commitment, not a side effect of open-sourcing the core.

The public version of this lives as a section on the marketing security page
(`web/src/app/(marketing)/docs/security/page.tsx`); keep the two in sync.

---

## The design already protects you

**Execution never depends on us.** The CLI, execution kernel, and adapters are licensed
Apache-2.0 and run entirely inside your own infrastructure — your machine or your CI runner.
They need no account and make no network call to any ReleaseTwin service to run a case or a
flag-proof. A hosted outage, a billing lapse, or the hosted platform disappearing altogether
does not block a release.

**Your data is portable.** Run history and evidence you have uploaded are exportable at any
time, in a documented JSON format, with no proprietary transformation you would need us to
reverse. Evidence documents are stored exactly as your CLI redacted them.

**The hosted platform is a control plane, not a runtime.** It holds accounts, tokens, run
history, and the evidence viewer. Losing it costs you the dashboard and the hosted history — not
the ability to run ReleaseTwin or the evidence you have already pulled out.

**Payments run through a Merchant of Record (Polar).** Polar is the seller of record: it
collects payment, remits sales tax / VAT, and issues invoices. Card and billing-address data are
entered only on Polar's hosted checkout and portal — the hosted platform never sees or stores
them. Cancelling is self-serve from Polar's portal; a lapsed or cancelled subscription degrades
hosted entitlements on a published grace schedule but never deletes your uploaded evidence, and
never affects the CLI running in your own infra.

---

## What we commit to if the company winds down

If we make a public decision to cease operating the hosted platform:

1. **At least 90 days' notice** to every account with an active plan, by email, before any
   shutdown date.
2. **Active hosted licenses convert to perpetual** for the remainder of their paid term — no
   further charge, continued access to your data for export throughout the notice period.
3. **The hosted source (`hosted/` and `web/`) is published** under a permissive or
   source-available license, so a customer or a third party can self-host the control plane.
4. **A final data export** is made available to every account: run history and all stored
   evidence, in the documented format.

## What this does not cover

- Ordinary paid downtime or incident response — see the status page and SLA terms for that.
- A pivot or feature deprecation that is not a wind-down. Normal product change is governed by
  the change/deprecation policy, not this document.
- Third-party dependencies (your cloud, your CI, your flag provider) — those are yours to manage
  and are exactly why execution is designed to stay in your infra.

---

## Why we can offer this and funded competitors usually can't

A venture-backed vendor's valuation depends on switching costs. Committing in advance to open the
source and hand back the data on the way out works against that model. For an independent,
self-funded team the incentive is a renewal, not a lock-in — so making the exit safe for the
customer costs us almost nothing and removes the single biggest objection to trusting a small
vendor with release-critical tooling.
