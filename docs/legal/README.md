# Legal documents

Contract drafts for `company-and-domain-launch` task 6.7. Two documents:

| File | Use | Based on |
|---|---|---|
| [`pilot-agreement.md`](pilot-agreement.md) | The agreement a design partner signs before a pilot. Short, free, evaluation-only, no purchase obligation. | Common Paper **Design Partner Agreement** structure (CC-BY 4.0) |
| [`dpa.md`](dpa.md) | Data Processing Agreement — attaches to the pilot agreement (and later to an MSA / the online Terms). Partner is controller, ReleaseTwin is processor. | Common Paper / Bonterms **DPA** structure + EU SCC references (CC-BY 4.0) |

> [!IMPORTANT]
> **These are drafts, not legal advice, and not ready to send to a customer.**
> They are a starting point filled in with ReleaseTwin's real facts (the operator
> is a Mexican persona física; the hosted Service processes run *metadata* by
> default, evidence only on opt-in and CLI-redacted; the subprocessors are AWS,
> Clerk, Polar, Amazon SES). A qualified Mexican technology lawyer must review both
> before use — specifically the **governing-law / venue** choice, the
> **limitation-of-liability** wording, the **international-transfer** mechanism for
> EU/UK customer data (Mexico has no EU adequacy decision), and how these interact
> with the AGPL-3.0 + Adapter Linking Exception + BSL 1.1 licensing stack.

## How the pieces fit

```
Pilot / design partner
        │
        ├── Pilot Agreement  ── evaluation licence, feedback, confidentiality, "as is", liability cap
        │
        └── DPA (Exhibit A)  ── controller/processor terms, subprocessors, security, breach, deletion
                                  └── Annex 1  processing details (what data, why, how long)
                                  └── Annex 2  technical & organisational security measures
                                  └── Annex 3  subprocessors
                                  └── Annex 4  EU SCCs (when the partner's data is EU/UK personal data)
```

For self-serve customers later, the same DPA is referenced from the online Terms;
enterprise deals get an MSA with the DPA as an exhibit. The **`/terms`** and
**`/privacy`** pages (in `web/src/app/(marketing)/`) are the self-serve
click-through layer and are consistent with these documents.

## Placeholders to resolve with counsel

- `[OPERATOR LEGAL NAME]` / `[RFC]` / `[OPERATOR ADDRESS]` — the exact SAT registration.
- `[GOVERNING LAW]` / `[VENUE]` — recommended default: the laws of the United Mexican States, courts of `[operator's city/state]`. Counsel confirms.
- `[LIABILITY CAP]` — pilot is free, so a low fixed cap (e.g. US$100 / MX$2,000). Counsel confirms enforceability.
- `[PILOT TERM]` — 60 or 90 days.
- `[NOTICE ADDRESS]` — `legal@releasetwin.com` once the mailbox exists.
