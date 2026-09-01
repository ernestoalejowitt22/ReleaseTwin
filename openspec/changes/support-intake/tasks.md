## 1. Routing doc

- [ ] 1.1 Write `SUPPORT.md` at the repo root: routing table (bug → GitHub issue;
      security → advisory, link `SECURITY.md`; billing / account / legal →
      support address; sales / pilot → support address with subject hint), the
      "acknowledgement in a few days, no formal SLA" expectation, and the
      address-indirection wording from design decision 2.
- [ ] 1.2 Add a one-line `## Support` section to `README.md` linking `SUPPORT.md`.
- [ ] 1.3 Update `CONTRIBUTING.md` "Open an issue first" bullet to point at the
      new issue templates / `SUPPORT.md` instead of a bare issue link.

## 2. GitHub issue templates

- [ ] 2.1 `.github/ISSUE_TEMPLATE/bug_report.yml` (issue form): surface picker
      (CLI / adapter / hosted API / web), ReleaseTwin version, repro steps,
      expected vs actual, logs/output field.
- [ ] 2.2 Add the required "could this cause a wrong verdict or leak
      fixture/secret/evidence content?" checkbox to the bug form, with intro
      text redirecting a yes to the security advisory flow (design decision 3).
- [ ] 2.3 `.github/ISSUE_TEMPLATE/feature_request.yml` (issue form): problem,
      proposed behavior, which surface, whether it needs a new adapter.
- [ ] 2.4 `.github/ISSUE_TEMPLATE/config.yml`: `blank_issues_enabled: false` and
      contact links for security (advisory URL) and account/billing (support
      address), with a `TODO(company-and-domain-launch)` comment on the email.

## 3. Honest tier support copy

- [ ] 3.1 Edit `hosted/plans.json` `support` strings: Free →
      `Community · GitHub issues`; Team → `Email · best-effort, ~2 business days`;
      Enterprise → `Priority email` (design decision 4).
- [ ] 3.2 Run `dotnet test` for `PlanCatalogTests` and the `web` `plans.ts`
      shape check; confirm both still pass.
- [ ] 3.3 In a running `web` build, verify the pricing page and features page
      render the new strings and that no other copy still says "shared Slack" or
      promises an SLA (grep `web/` for `Slack`, `SLA`).

## 4. Operator runbook

- [ ] 4.1 Write `docs/support.md`: label scheme (`bug`, `triage`, `needs-info`,
      `wontfix`, `security`, `question`), triage-sweep cadence (user sets the
      interval — see design Open Questions), how a billing/account email is
      handled and recorded, and the issue → direct-email escalation trigger.
- [ ] 4.2 Cross-link `docs/support.md` from `SUPPORT.md` (operator note) and
      from `docs/` index if one exists.

## 5. Cross-reference and close-out

- [ ] 5.1 Add one checklist line to `go-public-sequence`'s "announcement
      readiness" step: "`SUPPORT.md` + issue templates live and verified — see
      `support-intake`". Coordinate with that change's state; do not renumber
      its existing tasks.
- [ ] 5.2 Add a task to `company-and-domain-launch` (or note it there): when the
      site contact constants are split, grep `SUPPORT.md`, `docs/support.md`,
      and `.github/` for the old Gmail and swap to `support@` / `hello@`.
- [ ] 5.3 Run `openspec validate support-intake --strict`.

## 6. Needs the user to run this

- [ ] 6.1 Set the final triage-sweep interval in `docs/support.md` before merge.
- [ ] 6.2 After merge + repo goes public (per `go-public-sequence`): open a
      throwaway test issue via each template, confirm the forms render and
      `config.yml` contact links work, then close it.
