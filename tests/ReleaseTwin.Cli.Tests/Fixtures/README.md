# Evidence document fixtures

`local-evidence-viewer`: these two files are the redacted evidence document exactly as
`LocalEvidenceWriter` writes `<evidence-dir>/<case-id>/evidence.json` — camelCase keys in
`EvidenceDocument` record-declaration order, and keys whose value is null **omitted entirely**
(the writer serializes with `NullValueHandling.Ignore`).

One caveat on that, verified against a real run: `NullValueHandling.Ignore` governs the
document's *own* fields. A step's `adapter` payload is adapter-defined and passed through as a
pre-built `JToken`, so a real document can carry nulls and PascalCase keys inside it (the HTTP
adapter writes `"RequestBody": null`). These fixtures keep camelCase, null-free adapter payloads
on purpose — nothing rendering-side depends on the adapter's internal shape, and a fixture with
no null anywhere makes drift toward the hosted API's null-tolerant form fail loudly.

| File | Shape |
| --- | --- |
| `evidence-document.json` | An ordinary case: one leg, `leg` key absent. |
| `evidence-document-flag-proof.json` | A flag-proof case: `known-bad` then `known-good`. |

Those are the only two shapes `EvidenceRedactor.Redact` produces — it emits either a single
unnamed leg or the known-bad/known-good pair, never a mix. Both files deliberately include steps
that omit `assertion`, `adapter`, and `screenshots` and steps that carry them, so the viewer's
tolerance of absent optional keys is pinned rather than accidental.

## These are twins — keep them byte-identical

Each file has a counterpart under `web/src/test/fixtures/` in the platform repo
(`ernestoalejowitt22/releasetwin-platform`), which the hosted dashboard's evidence drill-down is
tested against. The claim in the public docs — that the dashboard renders the same evidence
document the local viewer does — is only true while the two copies match. Change one, change both.

`EvidenceViewerFixtureTests` asserts the on-disk properties directly, so a copy that drifts to
explicit nulls fails there rather than passing every other test.

Neither file contains a credential, a token, or an unredacted response body; the `«redacted»`
strings are the CLI's own mask value.
