# cases-naha — a real-target example

`naha-login-then-me.yaml` is the non-simulated version of the login-then-authenticated-call
pattern that [`../cases/example-auth-chain.yaml`](../cases/example-auth-chain.yaml) demonstrates
against public echo services. It drives a real deployment of the NAHA app (a separate project).

It does **not** run out of the box — it needs three values, none committed here:

| `${VAR}` | What |
|---|---|
| `NAHA_API_URL` | Base URL of the NAHA API deployment |
| `NAHA_ADMIN_EMAIL` | An admin account on that deployment |
| `NAHA_E2E_SECRET` | The shared `x-e2e-secret` for the e2e login endpoint (only present when that deploy sets `E2E_AUTH_ENABLED=true`) |

Supply them as environment variables, or store them as project secrets on the hosted platform
and let the CLI resolve `${VAR}` from there at run time.

It's kept as a reference for what a real target case looks like — real base URL, a real captured
bearer token, a real protected endpoint — rather than something you'd copy verbatim.
