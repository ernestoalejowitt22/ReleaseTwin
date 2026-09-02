## 1. Hosted: ingest response returns a report URL

- [x] 1.1 Ingest endpoint (case + flag-proof) response body gains `reportUrl`
      (the report's evidence page) + `runUrl` (project dashboard), built from
      `Web:BaseUrl` + `projectId`/`reportId` via `ReportUrls(...)`.
- [x] 1.2 `reportUrl` is the evidence view (which itself renders a graceful "no
      evidence" state); returned on every accepted upload incl. the
      evidence-not-accepted (free-tier) path, alongside the existing
      `evidenceAccepted` signal.
- [x] 1.3 URLs contain only `projectId` + `reportId` GUIDs — no fixture content,
      body, or credential (asserted structurally by the tests).
- [x] 1.4 `EvidenceIngestApiTests`: metadata-only, evidence-not-accepted, and
      absolute-URL-when-`Web:BaseUrl`-set (flag-proof endpoint) — all green.

## 2. CLI: carry the URLs into the run summary

- [x] 2.1 `RunSummary`: `schemaVersion` → 2; `runUrl` (top level) + `evidenceUrl`
      (per `RunSummaryCase`), both `[JsonIgnore(WhenWritingNull)]` so a no-upload
      summary has no new keys — differs from v1 only by the version integer.
- [x] 2.2 `IngestClient` upload methods return `IngestUploadResult`
      (`EvidenceAccepted`, `ReportUrl`, `RunUrl`); `SendAsync` parses the ack for
      every path (was: only the evidence path); unparseable/older API → nulls.
- [x] 2.3 `CliRunner`: `AddCase` moved after the upload; `evidenceUrl` set only
      when evidence was sent **and** accepted; `runUrl` taken from the first
      successful upload. No token / failed upload → null; exit code unchanged.
- [x] 2.4 `CliRunnerSummaryEvidenceLinkTests` (5): URLs from response; evidenceUrl
      omitted when not accepted; no-upload has no url keys; upload failure leaves
      unset; older API without urls handled. Existing schema-version asserts
      bumped to 2.

## 3. Action: render the links

- [x] 3.1 `render.mjs`: `runUrl` → " · [View run](…)" in the comment header and
      `details_url` on the check payload.
- [x] 3.2 `render.mjs`: a notable case with `evidenceUrl` renders its id cell as
      a link to the evidence page.
- [x] 3.3 Every URL render guarded on presence; refactored to export pure
      `renderBody` / `checkPayload` with the side-effecting flow behind
      `main()` + an `import.meta.url` guard — script behavior unchanged.
- [x] 3.4 `render.test.mjs` (`node --test`, 6 cases): no-URL body unchanged, v1
      summary, runUrl link + details_url, evidenceUrl row link, no details_url
      without runUrl, missing-summary body. New `render-unit` job in
      `pr-annotations.yml`.

## 4. Docs

- [x] 4.1 `docs/ci.md`: sample summary bumped to v2 with `runUrl` / `evidenceUrl`;
      documented as optional, forward-compatible, upload-only; Credentials note
      that the token turns the annotation into dashboard links.
- [x] 4.2 `integrations/github-action/README.md`: Notes bullet on the token →
      "View run" + per-case evidence links, with the fork-PR caveat.

## 5. Verify + close-out

- [x] 5.1 `dotnet build ReleaseTwin.sln` (0 errors) + `dotnet test` green — 270
      across 7 suites (Cli.Tests 147 → 152).
- [x] 5.2 `dotnet test hosted/ReleaseTwin.Hosted.slnx` green — 382 (+3).
- [x] 5.3 `node --test integrations/github-action/render.test.mjs` — 6/6.
- [x] 5.4 `openspec validate pr-annotation-evidence-link --strict` passes.
- [x] 5.5 Confirm with the user before archiving.
