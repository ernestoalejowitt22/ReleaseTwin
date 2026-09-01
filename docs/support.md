# Support — operator runbook

The user-facing routing doc is [`SUPPORT.md`](../SUPPORT.md). This is how the
requests get handled.

## Channels

- **GitHub Issues** — system of record for bugs and feature requests. Public.
- **Security advisories** — private, per [`SECURITY.md`](../SECURITY.md).
- **Email** (the support address) — account, billing, legal, sales. Private,
  often tied to an org identity. Recorded here as a one-line note in the
  relevant GitHub issue *or* a private tracking doc when there's no public
  issue to attach it to.

No helpdesk tool. If a weekly-ish sweep stops keeping up, that's the signal to
reconsider — not before.

## Labels

| Label | Meaning |
|---|---|
| `bug` | Confirmed or plausible defect |
| `enhancement` | Feature request / design note |
| `triage` | Applied by the issue templates; remove once it's been read and categorized |
| `needs-info` | Waiting on the reporter; stale after ~2 weeks with no reply → close with a note |
| `security` | A report that came through a public issue but should have been private — move it to an advisory, scrub the issue, apply this label to the advisory |
| `question` | Usage question, not a bug or a feature |
| `wontfix` | Closed deliberately; the closing comment says why |

## Cadence

**Triage sweep every 3 business days.** Each sweep:

1. Every issue with `triage` — read it, confirm/reproduce or ask for info,
   swap `triage` for a real category label, remove `triage`.
2. `needs-info` issues past ~2 weeks with no reply → close with a note.
3. Skim new email: acknowledge each within the same 3-business-day target,
   even if the answer is "looking into it."

The acknowledgement target in `SUPPORT.md` and `SECURITY.md` is **3 business
days**. It is a target, not an SLA — keep the wording "best-effort" everywhere.

## Escalation: issue → direct email

Move a thread to email when it involves:

- account-specific data (org id, billing, a customer's private config)
- anything a reporter asks to keep off a public issue
- a security report (→ advisory, not email, unless the reporter can't use
  advisories)

Leave a short public note on the original issue ("continuing this by email")
and close or keep it open depending on whether there's a public outcome to
record.

## When something's actually broken in production

That's not this process — it's the alerting path (`docs/operator-alerting.md`,
SNS → operator email) plus the incident being visible in the affected
customer's dashboard. Support intake is for things users report, not things
monitoring catches.
