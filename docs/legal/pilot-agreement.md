<!--
DRAFT — NOT LEGAL ADVICE — DO NOT SEND TO A CUSTOMER BEFORE COUNSEL REVIEW.
Structure adapted from the Common Paper Design Partner Agreement (CC-BY 4.0,
https://commonpaper.com/standards/design-partner-agreement/). Filled in with
ReleaseTwin facts. A qualified Mexican technology lawyer must review the
governing-law, liability, publicity and IP clauses before this is used.
-->

# ReleaseTwin Pilot Agreement

**This Pilot Agreement (the "Agreement") is between:**

- **Provider:** `[OPERATOR LEGAL NAME]`, a Mexican persona física con actividad
  empresarial (RFC `[RFC]`), of `[OPERATOR ADDRESS]`, operating the product
  "ReleaseTwin" ("**Provider**", "we", "us"); and
- **Partner:** `[PARTNER LEGAL NAME]`, of `[PARTNER ADDRESS]` ("**Partner**",
  "you").

**Effective Date:** `[DATE]`

Provider and Partner are each a "party" and together the "parties".

---

## 1. Purpose

Partner wants to evaluate ReleaseTwin for its release-testing workflow during a
time-boxed pilot, and Provider wants Partner's feedback to improve the product.
This Agreement covers that pilot only. It is **not** a commitment by either party
to a paid subscription, and it does not oblige Partner to buy anything.

## 2. The Service

"**Service**" means the ReleaseTwin hosted control plane — the web dashboard, the
ingest and management API, and the account system — made available at
Provider's domain.

The ReleaseTwin **command-line interface, execution engine, and adapters** are
open-source software licensed separately (AGPL-3.0 with the Adapter Linking
Exception; see the source repository). They run in Partner's own infrastructure,
this Agreement does not govern their use, and Partner may run them with or without
the Service.

## 3. Pilot term

3.1 The pilot runs for **`[PILOT TERM]` days** from the Effective Date (the
"**Pilot Period**"), unless extended by written agreement (email is fine) or ended
earlier under Section 11.

3.2 At the end of the Pilot Period the parties may (a) enter a paid subscription
under Provider's then-current terms, (b) agree in writing to extend the pilot, or
(c) let this Agreement expire. If nothing is agreed, it expires and Section 11.2
applies.

## 4. Licence and access

4.1 During the Pilot Period, Provider grants Partner a limited, non-exclusive,
non-transferable, non-sublicensable right for Partner and its employees and
contractors to access and use the Service for Partner's **internal evaluation**.

4.2 Provider will create Partner's organisation, issue initial credentials, and
give Partner reasonable onboarding help. Partner is responsible for activity under
its account and for keeping its API tokens and credentials secret.

4.3 Partner will not: resell, sublicense, or make the Service available to a third
party; probe, load-test, or attempt to defeat the Service's security or
tenant-isolation; use it in violation of law; or use it to build a competing
product.

## 5. Partner data

5.1 **Data minimisation.** By design the Service receives only run *metadata*
(case identifiers, oracle references, fixture hashes, pass/fail, failure
classification, timestamps) unless Partner explicitly enables evidence upload for
a project. Partner should, wherever practical, run the pilot against
**non-production test data** and should not upload evidence containing personal
data or secrets it does not need Provider to hold for the evaluation. Evidence, if
enabled, is redacted by the CLI in Partner's own environment before it is sent.

5.2 **Data processing.** The **Data Processing Agreement in Exhibit A** governs
Provider's processing of any personal data contained in Partner data. Partner is
the controller; Provider is the processor.

5.3 **Partner owns its data.** As between the parties, Partner owns all data it
submits. Provider will use it only to provide the Service, keep it secure, and —
in aggregate and de-identified form — improve the product.

5.4 **Return / deletion.** On expiry or termination, Partner may export its data
from the dashboard for **30 days**, after which Provider will delete it, subject
to Exhibit A and any residual backup copies that are then overwritten on the
ordinary cycle.

## 6. Feedback

6.1 Partner is encouraged, but not obliged, to give feedback — bug reports,
feature requests, usability observations, and comments on fit.

6.2 Partner grants Provider a perpetual, irrevocable, worldwide, royalty-free,
sublicensable licence to use, act on, and incorporate that feedback into the
Service and any Provider product, with no obligation or attribution. Provider owns
all improvements it makes to its own products, including those prompted by
feedback. This does not give Provider any right in Partner's own products, data,
or confidential information.

## 7. Confidentiality

7.1 "**Confidential Information**" means non-public information a party ("Discloser")
shares with the other ("Recipient") that is marked confidential or that a
reasonable person would understand to be confidential — including, for Provider,
non-public features, roadmap, pricing, and the Service's internals; and for
Partner, its data, systems, and evaluation results.

7.2 Recipient will use Confidential Information only to perform this Agreement,
protect it with at least reasonable care, and not disclose it except to its
personnel and advisers who need it and are under confidentiality obligations at
least as protective.

7.3 The obligations do not apply to information that is or becomes public through
no fault of Recipient, was rightfully known to Recipient without a duty of
confidence, is independently developed, or is rightfully received from a third
party. Recipient may disclose if legally compelled, giving the Discloser prior
notice where lawful.

7.4 These obligations last during the term and for **three (3) years** after,
except that trade secrets remain protected for as long as they are trade secrets.

## 8. Intellectual property

Each party keeps all right, title, and interest in what it owned before or
develops outside this Agreement. Provider owns the Service and all ReleaseTwin
software and marks. Partner owns its data and its own systems and products.
Nothing here transfers ownership.

## 9. No warranty during the pilot

9.1 The Service is provided **"AS IS" and "AS AVAILABLE"** for the pilot. Provider
makes no warranty of any kind — express, implied, or statutory — including
merchantability, fitness for a particular purpose, non-infringement, uptime,
accuracy, or that the Service will be uninterrupted or error-free.

9.2 There is **no service-level agreement, no support commitment, and no uptime
target** during the pilot. Features may change, be withdrawn, or break. The
Service is not a substitute for Partner's own release process or judgement; a
passing ReleaseTwin result is evidence, not a guarantee.

## 10. Limitation of liability

10.1 **Excluded damages.** Neither party is liable for indirect, incidental,
special, consequential, exemplary, or punitive damages, or for lost profits,
revenue, data, or goodwill, arising out of this Agreement, even if advised of the
possibility.

10.2 **Cap.** Each party's total aggregate liability arising out of or related to
this Agreement will not exceed **`[LIABILITY CAP]`** (e.g. US$100). The pilot is
provided free of charge; this cap reflects that.

10.3 **Carve-outs.** Sections 10.1 and 10.2 do not apply to: a party's breach of
Section 7 (Confidentiality); Partner's breach of Section 4.3; a party's
infringement or misappropriation of the other's intellectual property; a party's
gross negligence, fraud, or wilful misconduct; or any liability that cannot be
limited or excluded under applicable law.

## 11. Term and termination

11.1 This Agreement starts on the Effective Date and runs for the Pilot Period.
Either party may terminate for convenience on **`[7]` days'** written notice, and
either party may terminate immediately on written notice if the other materially
breaches and does not cure within `[10]` days of notice.

11.2 **On expiry or termination:** Partner's access to the Service ends; each
party returns or destroys the other's Confidential Information on request;
Section 5.4 (export/deletion) applies; and Sections 5.2, 5.3, 6, 7, 8, 9, 10, 11.2,
12, and 13 survive.

## 12. Publicity

Neither party will use the other's name, logo, or marks in any public statement,
customer list, or marketing without the other's prior written consent. Consent to
a specific use may be given by email and does not extend to other uses.

## 13. General

13.1 **Governing law and venue.** This Agreement is governed by `[GOVERNING LAW —
recommended: the laws of the United Mexican States]`, without regard to conflict-of-law
rules, and the parties submit to the exclusive jurisdiction of the courts of
`[VENUE — recommended: the operator's domicile]`. The UN Convention on Contracts for
the International Sale of Goods does not apply.

13.2 **Entire agreement; order of precedence.** This Agreement and its Exhibit A
are the entire agreement on this subject and supersede prior discussions. If
Exhibit A conflicts with the body of this Agreement on data protection, Exhibit A
controls.

13.3 **Amendments and waiver.** Changes must be in a writing signed (electronic
signature or email confirmation is acceptable) by both parties. A failure to
enforce a term is not a waiver.

13.4 **Assignment.** Neither party may assign this Agreement without the other's
consent, except to a successor of all or substantially all of its business or
assets on notice.

13.5 **Notices.** Notices go to the email addresses the parties designate below
and are effective on receipt. Provider's notice address: `[NOTICE ADDRESS —
legal@releasetwin.com]`.

13.6 **Independent contractors.** The parties are independent contractors. This
Agreement creates no partnership, agency, or employment relationship.

13.7 **Force majeure.** Neither party is liable for a delay or failure caused by
events beyond its reasonable control.

13.8 **Severability.** If a term is unenforceable, it is modified to the minimum
extent necessary and the rest stays in effect.

---

**Agreed:**

| Provider | Partner |
|---|---|
| Signature: ________________________ | Signature: ________________________ |
| Name: `[OPERATOR LEGAL NAME]` | Name: |
| Title: Owner | Title: |
| Date: | Date: |

---

## Exhibit A — Data Processing Agreement

The Data Processing Agreement at [`dpa.md`](dpa.md) is incorporated into this
Agreement as Exhibit A.
