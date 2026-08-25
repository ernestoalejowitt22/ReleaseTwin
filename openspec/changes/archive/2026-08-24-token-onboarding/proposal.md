## Why

A customer who signs up, creates a project, and issues an API token today sees only the bare token string with "copy it now" — no indication of what to do with it. Every other piece of the integration path (authoring cases, setting credentials, wiring CI) already works; the token is the one connective step the product currently abandons the customer at, right after the moment they're most engaged (just finished signup).

## What Changes

- When a token is issued, show it alongside the exact `export RELEASETWIN_API_TOKEN=...` command and a copy-paste CLI invocation (install + run) for that project.
- Make explicit, in-product, that setting this token is optional and customer-controlled: running without it stays fully local and free; setting it links runs to this project/organization in the dashboard.
- Surface a zero-credential example case (the existing `examples/cases/example-http.yaml`) as the suggested first run, so the copy-pasted command has something real to execute immediately.

## Capabilities

### Modified Capabilities
- `dashboard`: token issuance now includes install/run instructions and an example-case pointer alongside the token value, not just the bare token.

## Impact

- `web/src/app/dashboard/issue-token-button.tsx`: extend the post-issuance display.
- `web/src/app/dashboard/actions.ts` / hosted API token-issuance response: may need to carry enough context (e.g. project id/name) for the frontend to render the exact invocation — no new persisted data, display-only.
- Depends on `cli-packaging` for the exact install command shown (Docker vs. dotnet tool); can ship with a placeholder/source-build instruction first and update once packaging lands, or sequence after it.
- No backend data model changes.
