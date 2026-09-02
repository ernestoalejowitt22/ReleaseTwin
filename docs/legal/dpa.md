<!--
DRAFT — NOT LEGAL ADVICE — DO NOT SEND TO A CUSTOMER BEFORE COUNSEL REVIEW.
Structure adapted from the Common Paper DPA and the Bonterms DPA (both CC-BY 4.0).
Filled in with ReleaseTwin facts. A qualified lawyer must review the roles,
international-transfer mechanism (Mexico has no EU adequacy decision — EU/UK
personal data needs SCCs / the UK Addendum, referenced in Annex 4), the security
measures in Annex 2, and the subprocessor list in Annex 3 before use.
-->

# Data Processing Agreement

This Data Processing Agreement ("**DPA**") forms part of the agreement between
`[OPERATOR LEGAL NAME]` ("**Provider**", the "**Processor**") and the customer
identified in that agreement ("**Customer**", the "**Controller**") for Customer's
use of the ReleaseTwin hosted Service (the "**Principal Agreement**"). It governs
Provider's processing of Customer Personal Data.

If there is a conflict between this DPA and the Principal Agreement on the
processing of Personal Data, this DPA controls.

## 1. Definitions

1.1 "**Data Protection Law**" means all laws on the processing of personal data
that apply to a party, including, as applicable: the EU General Data Protection
Regulation 2016/679 ("**EU GDPR**"), the UK GDPR and Data Protection Act 2018,
the Swiss FADP, the California Consumer Privacy Act as amended ("**CCPA**"), and
Mexico's Ley Federal de Protección de Datos Personales en Posesión de los
Particulares ("**LFPDPPP**").

1.2 "**Customer Personal Data**" means Personal Data that Provider processes on
Customer's behalf under the Principal Agreement. Its categories, data subjects,
and purposes are described in **Annex 1**.

1.3 "**Personal Data**", "**processing**", "**controller**", "**processor**",
"**data subject**", "**personal data breach**", and "**supervisory authority**"
have the meanings in the EU GDPR (and the equivalent terms in other Data
Protection Law). "**Subprocessor**" means a processor engaged by Provider to
process Customer Personal Data.

1.4 "**Standard Contractual Clauses**" or "**SCCs**" means the clauses in
Commission Implementing Decision (EU) 2021/914, and, for UK data, the UK
Information Commissioner's International Data Transfer Addendum.

## 2. Roles and scope

2.1 As between the parties, **Customer is the controller and Provider is the
processor** of Customer Personal Data. Where Customer is itself a processor acting
for a third-party controller, Provider is a subprocessor and Customer is
responsible for the third-party controller's authorisations and instructions.

2.2 Provider processes Customer Personal Data only:
  (a) to provide, secure, and support the Service under the Principal Agreement;
  (b) on Customer's documented instructions, of which the Principal Agreement,
      this DPA, and Customer's use and configuration of the Service are the
      complete set; and
  (c) as required by a law that applies to Provider, in which case Provider will
      tell Customer first unless that law prohibits it.

2.3 Provider will tell Customer if, in its opinion, an instruction infringes Data
Protection Law. Provider is not responsible for determining whether Customer's
instructions comply with law.

2.4 **Service design limits the data.** By default the Service receives only run
*metadata* (see Annex 1). Evidence documents — which may contain Personal Data —
are processed only for projects Customer opts in, and are redacted by the CLI in
Customer's environment before transmission. Provider does not sell Customer
Personal Data and does not process it for advertising or for its own purposes
except to produce aggregate, de-identified statistics about Service use.

## 3. Confidentiality and personnel

Provider ensures that anyone it authorises to process Customer Personal Data is
under a duty of confidentiality and processes the data only on instructions.

## 4. Security

4.1 Provider implements and maintains the technical and organisational measures in
**Annex 2**, appropriate to the risk, including the pseudonymisation and
encryption of Personal Data, and measures for the confidentiality, integrity,
availability, and resilience of processing systems.

4.2 Provider periodically tests and reviews those measures and may update them,
provided the level of protection is not materially reduced.

## 5. Subprocessors

5.1 Customer gives **general authorisation** for Provider to engage the
Subprocessors listed in **Annex 3** and future Subprocessors added under this
Section.

5.2 Provider imposes on each Subprocessor, by written contract, data-protection
obligations no less protective than those in this DPA, and remains liable to
Customer for a Subprocessor's failure to meet them.

5.3 Provider will give Customer **at least 30 days' notice** (by email or a
subscribed change feed) before adding or replacing a Subprocessor that processes
Customer Personal Data. Customer may object on reasonable data-protection grounds
within that period; the parties will work in good faith to resolve it, and if they
cannot, Customer may terminate the affected part of the Service without penalty.

## 6. Data subject requests

6.1 Provider will, on request, provide reasonable assistance for Customer to
respond to data-subject requests to exercise rights under Data Protection Law
(access, rectification, erasure, restriction, portability, objection), taking into
account the nature of the processing.

6.2 If Provider receives such a request directly, it will not respond except to
confirm the request relates to Customer, and will forward it to Customer without
undue delay. Customer can service most requests itself through the dashboard's
export and delete functions.

## 7. Assistance

Provider will provide reasonable assistance to Customer with data protection
impact assessments and prior consultations with a supervisory authority, taking
into account the nature of processing and the information available to Provider.

## 8. Personal data breach

Provider will notify Customer **without undue delay, and in any event within 72
hours**, after becoming aware of a personal data breach affecting Customer
Personal Data. The notice will describe the nature of the breach, the categories
and approximate number of data subjects and records affected (to the extent
known), the likely consequences, and the measures taken or proposed. Provider will
cooperate with Customer and take reasonable steps to mitigate.

## 9. Return and deletion

9.1 On termination or expiry of the Principal Agreement, Provider will, at
Customer's choice, delete or return Customer Personal Data, and delete existing
copies, within **30 days**, unless a law that applies to Provider requires
retention.

9.2 Evidence documents are additionally subject to each project's retention window
(default 30 days, configurable up to 365) enforced by an automated daily purge
during the term.

9.3 Backups containing Customer Personal Data are overwritten on Provider's
ordinary backup cycle after deletion and are not restored except for
disaster-recovery, during which they remain protected by this DPA.

## 10. Audit

10.1 Provider will make available to Customer the information reasonably necessary
to demonstrate compliance with this DPA, including third-party audit reports or
security summaries where Provider has them.

10.2 If that information is not sufficient, Customer (or an independent auditor it
appoints, who is not a competitor of Provider and is under confidentiality) may
audit Provider's processing, **once per 12-month period** (unless required more
often by a supervisory authority), on at least 30 days' written notice, during
business hours, without unreasonable disruption, and at Customer's cost. The
parties will agree the scope in advance.

## 11. International transfers

11.1 Provider is established in **Mexico** and hosts the Service on infrastructure
in the **United States** (see Annex 3). Using the Service therefore involves
transferring Customer Personal Data outside the EEA, the UK, and Switzerland.

11.2 **Where Customer Personal Data is protected by the EU GDPR, UK GDPR, or Swiss
FADP**, the parties enter the **SCCs** as set out in **Annex 4**, which are
incorporated by reference and prevail over any conflicting term of this DPA for
such data. Provider will also make available transfer-impact information on
request and adopt supplementary measures where required.

11.3 For Customer Personal Data protected only by the LFPDPPP or the CCPA, the
parties rely on this DPA and the Principal Agreement as the transfer safeguard,
and Provider processes such data as a "service provider" / "processor" that does
not "sell" or "share" it.

## 12. Liability

Each party's liability under this DPA is subject to the limitations and exclusions
of liability in the Principal Agreement. Nothing in this DPA limits either party's
liability to a data subject or a supervisory authority under Data Protection Law.

## 13. General

13.1 This DPA takes effect on the effective date of the Principal Agreement and
continues while Provider processes Customer Personal Data.

13.2 This DPA is governed by the same law and venue as the Principal Agreement,
except that the SCCs are governed by the law they specify.

13.3 If a change in Data Protection Law requires a change to this DPA, the parties
will negotiate an amendment in good faith.

---

## Annex 1 — Description of processing

| Item | Detail |
|---|---|
| **Subject matter** | Provision of the ReleaseTwin hosted control plane (dashboard, ingest/management API, account system). |
| **Duration** | For the term of the Principal Agreement, plus the deletion period in Section 9. |
| **Nature and purpose** | Storing and displaying run history and (opt-in) evidence; authenticating users; enforcing plan limits; securing the Service; billing (paid plans); sending transactional and, if opted in, product email. |
| **Types of Personal Data** | **Account / identity** (via Clerk): user email address, display name if provided, authentication events. **Team membership**: member email addresses and roles. **Notification / share configuration**: webhook URLs, invited email addresses. **Operational**: IP address and user agent on API/dashboard requests, error diagnostics, server logs. **Evidence (opt-in only)**: whatever Personal Data survives the CLI-side redaction in the request/response summaries and screenshots Customer chooses to upload — Customer controls this. |
| **Special category data** | None is intended or required. Customer must not upload special-category data in evidence. |
| **Data subjects** | Customer's personnel and contractors with a ReleaseTwin account; individuals whose data appears in Customer's test fixtures or captured evidence only if Customer opts a project into evidence upload and that data is not redacted. |
| **Frequency** | Continuous during the term (API uploads from Customer's CI; dashboard access). |
| **Controller** | Customer. |
| **Processor** | Provider. |

**What the Service does NOT receive by default:** Customer's fixture file
contents, request or response bodies, credentials, source code, or CI
configuration. The default upload contract has no field for them; they never
leave Customer's infrastructure unless Customer enables evidence upload.

## Annex 2 — Technical and organisational security measures

| Area | Measure |
|---|---|
| **Encryption in transit** | HTTPS/TLS only on every endpoint. The API is served over an HTTPS-only transport (no plaintext listener); the app enforces HTTPS redirection and HSTS. Auth and web surfaces terminate TLS with managed certificates. |
| **Encryption at rest** | The primary datastore (Amazon DynamoDB) and the evidence object store (Amazon S3, SSE) are encrypted at rest. Stored project secrets and adapter credentials are additionally encrypted with an application-level key ring held in AWS Systems Manager Parameter Store (KMS-encrypted), separate from the database. |
| **Secrets handling** | API tokens are high-entropy opaque values; only a SHA-256 hash is stored; the plaintext is shown once. Card and billing data are entered only on the Merchant of Record's surface and never reach Provider. |
| **Tenant isolation** | Every request is scoped to the caller's organisation from verified session/token claims, never from a client-supplied identifier; role-based access control (admin / member / viewer). Evidence blob storage is namespaced per project; the CLI ingest token cannot address another project. |
| **Authentication** | Delegated to a dedicated identity provider (Clerk). Two distinct, non-interchangeable auth domains: web session (JWT) and CLI API token. |
| **Egress / SSRF controls** | Customer-supplied webhook URLs are validated (HTTPS only, no private / loopback / link-local / cloud-metadata addresses) at save time and again at send time, with the outbound connection pinned to the validated address; outbound HTTP follows no redirects and times out quickly. |
| **Abuse controls** | Per-caller rate limiting on the ingest, share-link, and billing-webhook surfaces; billing webhooks are signature-verified with a timestamp-freshness check. |
| **Data lifecycle** | Per-project evidence retention windows (default 30 days, up to 365) enforced by an automated daily purge; self-service data export; account data deleted within 30 days of account deletion. |
| **Logging & monitoring** | Structured request logging to the cloud provider's log service; error and rate alarms with operator alerting. |
| **Change management & deployment** | Infrastructure as code; CI-only deployment via short-lived federated credentials (OIDC), no long-lived cloud keys; dependency-vulnerability and static-analysis scanning and secret scanning on every change. |
| **Personnel** | Access limited to the operator; confidentiality obligations; least-privilege cloud IAM roles per function. |
| **Sub-processing** | Written data-protection terms with every subprocessor (Annex 3). |

## Annex 3 — Subprocessors

| Subprocessor | Purpose | Location of processing | Customer Personal Data |
|---|---|---|---|
| **Amazon Web Services, Inc.** | Compute (Lambda), database (DynamoDB), object storage (S3), email (SES), secret storage (SSM) | United States (us-east-1) | All categories in Annex 1 |
| **Clerk, Inc.** | Authentication and identity | United States | Account / identity data |
| **Polar Software Inc.** (Merchant of Record) | Subscription billing and invoicing — **paid plans only** | United States / EU | Billing contact and account email; card/billing data is entered on Polar's surface and not received by Provider |
| **Amazon SES** (part of AWS above) | Transactional and product email | United States (us-east-1) | Recipient email addresses |
| **Vercel Inc.** | Hosting of the public marketing site | United States / global edge | None of the categories above (the marketing site does not process Customer Personal Data of the hosted Service) |

Provider maintains the current list and notifies changes under Section 5.3.

## Annex 4 — Standard Contractual Clauses (EU / UK / Swiss data)

Where Annex 1 data is protected by the EU GDPR, UK GDPR, or Swiss FADP, the parties
enter the SCCs (Commission Implementing Decision (EU) 2021/914), **Module Two
(controller to processor)**, as follows:

- **Clause 7 (docking clause):** applies.
- **Clause 9 (subprocessors):** Option 2 (general written authorisation); the
  notice period is 30 days (DPA Section 5.3).
- **Clause 11 (redress):** the optional independent-dispute-resolution language
  does **not** apply.
- **Clause 17 (governing law):** the law of `[EU MEMBER STATE — e.g. Ireland]`.
- **Clause 18 (forum):** the courts of `[same EU MEMBER STATE]`.
- **Annex I.A (parties):** as in the Principal Agreement and this DPA.
- **Annex I.B (description of transfer):** as in Annex 1.
- **Annex I.C (competent supervisory authority):** the authority of the EU member
  state in Clause 17, or the lead authority for Customer.
- **Annex II (technical and organisational measures):** as in Annex 2.
- **Annex III (subprocessors):** as in Annex 3.

For **UK** data, the SCCs are amended by the UK ICO's International Data Transfer
Addendum (version B1.0), with Tables 1–3 completed from the above and Table 4
"Importer" selected. For **Swiss** data, references to the GDPR are read as
references to the FADP, "member state" includes Switzerland, and the Swiss FDPIC
is a competent supervisory authority.

> Counsel to confirm: the choice of EU member state law/forum, whether a
> transfer-impact assessment should be attached, and whether Mexico's status
> (no adequacy decision) requires supplementary measures beyond Annex 2 for
> a given customer.
