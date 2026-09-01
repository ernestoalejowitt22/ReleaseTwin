## 1. Routing doc

- [x] 1.1 `SUPPORT.md` at the repo root — routing table (bug/feature → issue forms;
      security → advisory; account/billing/legal + sales/pilot → support address),
      the "~3 business days, best-effort, no SLA" expectation, and the
      address-indirection wording (design decision 2).
- [x] 1.2 `## Support` section in `README.md` linking `SUPPORT.md`.
- [x] 1.3 `CONTRIBUTING.md` "Open an issue first" bullet now points at the issue
      chooser + `SUPPORT.md`.

## 2. GitHub issue templates

- [x] 2.1 `.github/ISSUE_TEMPLATE/bug_report.yml` (issue form): surface dropdown,
      version, what-happened, repro, minimal case, runtime, engine-vs-hosted,
      logs. Replaces the old `.md`.
- [x] 2.2 Required "this is not a security issue (wrong verdict / leaked
      fixture, secret, or evidence)" confirmation checkbox, with a markdown
      block above redirecting a yes to the advisory flow (design decision 3).
- [x] 2.3 `.github/ISSUE_TEMPLATE/feature_request.yml` (issue form): problem,
      who has it, rough shape, surface dropdown, alternatives, scope checkboxes.
- [x] 2.4 `.github/ISSUE_TEMPLATE/config.yml`: `blank_issues_enabled: false`
      already set; contact links now security + account/billing + sales, each
      with a `TODO(company-and-domain-launch)` on the `mailto:`. Dropped the
      Discussions link (design decision 1 — not enabling it on day one).

## 3. Honest tier support copy

- [x] 3.1 `hosted/plans.json` `support`: Free → `Community · GitHub issues`;
      Team → `Email · best-effort, ~3 business days`; Enterprise →
      `Priority email` (design decision 4, "3" to match the runbook interval).
- [x] 3.2 `dotnet test` hosted suite — 346 pass (19 plan-catalog). `web`
      `plans.ts` shape check passes implicitly (`next build` imports it and throws
      on a missing/non-string `support`).
- [x] 3.3 Local build: pricing page renders all three new strings;
      `git grep -nI "shared Slack|\bSLA\b" web/` returns nothing. (features page
      does not render `tier.support` — proposal was slightly off, no action.)

## 4. Operator runbook

- [x] 4.1 `docs/support.md`: label scheme (`bug`, `enhancement`, `triage`,
      `needs-info`, `security`, `question`, `wontfix`), **3-business-day triage
      sweep**, email handling + recording, issue → direct-email escalation, and
      the boundary vs the alerting path.
- [x] 4.2 `docs/support.md` cross-linked from `SUPPORT.md`; `SUPPORT.md` linked
      from `README.md` and `CONTRIBUTING.md`.

## 5. Cross-reference and close-out

- [x] 5.1 `docs/go-public-runbook.md` §5 (Announcement readiness) gains a
      checklist line pointing at this change, gated before the visibility flip.
- [x] 5.2 `company-and-domain-launch` task 6.5 extended: when the domain
      addresses work, grep + swap the Gmail in `SECURITY.md`, `SUPPORT.md`,
      `docs/support.md`, and `.github/ISSUE_TEMPLATE/config.yml`.
- [x] 5.3 `openspec validate support-intake --strict` passes.

## 6. Needs the user to run this

- [x] 6.1 Triage-sweep interval set to **3 business days** (user's call
      2026-09-01) — in `docs/support.md`, `SUPPORT.md`, and the Team tier copy.
- [ ] 6.2 After merge + repo goes public (per `go-public-sequence`): open a
      throwaway issue via each template, confirm the forms render and the
      `config.yml` contact links work, then close it.
