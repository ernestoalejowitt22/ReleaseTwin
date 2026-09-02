## 1. Hosted: ingest response returns a report URL

- [ ] 1.1 Ingest endpoint (case + flag-proof) response body gains a canonical
      absolute dashboard URL for the stored report, org-scoped.
- [ ] 1.2 URL resolves to the evidence view when evidence was accepted; to the
      report view otherwise. Not-accepted path still returns the URL + the
      existing distinct signal.
- [ ] 1.3 Assert the URL carries only org/report identifiers — no fixture
      content, body, or credential.
- [ ] 1.4 Hosted tests for all three cases.

## 2. CLI: carry the URLs into the run summary

- [ ] 2.1 `RunSummary`: `schemaVersion` → 2; add nullable `runUrl` (top level)
      and `evidenceUrl` (per `RunSummaryCase`); omit when null.
- [ ] 2.2 `IngestClient`: parse the URL from the ingest response; surface it
      (per-upload) to the caller.
- [ ] 2.3 `CliRunner` / `RunSummaryBuilder`: record each upload's URL against its
      case; derive `runUrl` (the run page). No token / failed upload → fields
      stay null; exit code and case outcomes unchanged.
- [ ] 2.4 CLI tests: with-token populates; no-token summary differs from v1 only
      by the version integer; upload failure leaves fields null.

## 3. Action: render the links

- [ ] 3.1 `render.mjs`: when `runUrl` is set, add a "View run" link to the comment
      header and set the check run `details_url`.
- [ ] 3.2 `render.mjs`: when a case row has `evidenceUrl`, render the row as a
      link.
- [ ] 3.3 `render.mjs`: guard every URL render on presence; a summary with no
      URLs (and a v1 summary) renders exactly as today.
- [ ] 3.4 Node test / fixture: v2 summary with URLs, v2 without, v1 — snapshot the
      rendered comment body + check payload.

## 4. Docs

- [ ] 4.1 `docs/ci.md`: note that setting `RELEASETWIN_API_TOKEN` turns the PR
      annotation into a link into the dashboard; document `runUrl` / `evidenceUrl`
      as optional, forward-compatible summary fields.
- [ ] 4.2 `integrations/github-action/README.md`: one line under the token/secrets
      section.

## 5. Verify + close-out

- [ ] 5.1 `dotnet build` + `dotnet test ReleaseTwin.sln` green — report counts.
- [ ] 5.2 Hosted test suite green — report counts.
- [ ] 5.3 Action render test green.
- [ ] 5.4 `openspec validate pr-annotation-evidence-link --strict` passes.
- [ ] 5.5 Confirm with the user before archiving.
