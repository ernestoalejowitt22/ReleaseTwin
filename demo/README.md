# Landing-page demo

`web/public/demo-flag-proof.svg` is the animated terminal recording embedded on the landing page
and the quickstart. It shows two real runs of the CLI:

1. **`demo/quickstart/`** — a zero-credential HTTP case against a live public API (`PASS HTTP-DEMO-1`).
2. **`demo/flag-proof/`** — a LaunchDarkly-backed flag-proof case whose pipeline reads the flag it
   just toggled, so the outcome is deterministically `FLAGPROOF CHECKOUT-FIX-1 (Passed)`.

## Re-recording

```bash
demo/record.sh
```

Needs: .NET 8+, [`asciinema`](https://asciinema.org) (`brew install asciinema`), `npx`, and either
`LAUNCHDARKLY_API_TOKEN` / `LAUNCHDARKLY_PROJECT_KEY` / `LAUNCHDARKLY_ENVIRONMENT_KEY` already
exported, or AWS credentials that can read `releasetwin/e2e/launchdarkly-account` from Secrets
Manager (the script fetches them itself).

The script builds a single-file CLI into `demo/.bin/` (gitignored), records to
`demo/flag-proof.cast` (committed — the render source), and renders the SVG with `svg-term-cli`.

## Files

| Path | Committed | What |
|---|---|---|
| `record.sh`, `scripts/session.sh` | yes | recorder + the scripted session it captures |
| `quickstart/`, `flag-proof/` | yes | the two case directories the demo runs |
| `flag-proof.cast` | yes | asciicast v2 recording — re-render the SVG from this |
| `../web/public/demo-flag-proof.svg` | yes | the rendered asset the site embeds |
| `.bin/`, `.env.local` | no | local build output, fetched credentials |
