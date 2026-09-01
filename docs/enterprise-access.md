# Enterprise access: VPN-isolated targets and Entra ID / org OAuth

> "Everything is behind our VPN and gated by organization Microsoft OAuth."

Two separate problems live in that sentence — **network reachability** and
**identity**. ReleaseTwin's architecture answers the first almost for free; the
second is a configuration recipe.

## Network: no inbound access is ever required

The engine is the CLI, and it runs **in your CI, on your runners**. The hosted
platform is ingest-only: the CLI pushes evidence and verdicts *up* over outbound
HTTPS. Nothing in the hosted platform ever connects *into* your network or the
target under test.

```
   your CI runner ──▶ target under test        (inside your VPN / VPC)
        │
        └──▶ app.releasetwin ...  (outbound HTTPS: evidence + verdicts only)
```

So there is **no allowlist entry, firewall rule, or reverse tunnel** to grant
ReleaseTwin — there is nothing to grant.

### Reaching a network-isolated target

Run the CLI where the target is already reachable:

| Runner | Isolated target reachable? |
|---|---|
| **Self-hosted runner** inside the VPN / VPC (GitHub Actions, Azure Pipelines, GitLab) | Yes — this is the supported path |
| Cloud-hosted runner + a **customer-operated** tunnel (WireGuard, Cloudflare, an SSH forward) on the runner | Yes, if you operate the tunnel |
| Cloud-hosted runner, no tunnel | No — the target's network isn't reachable |

A runner that cannot reach the target reports the affected operations as
unreachable-dependency / inconclusive — never as a product failure.

## API auth: Microsoft Entra ID

The HTTP adapter performs a standard OAuth2 client-credentials exchange as one
step. Point it at your tenant's v2 token endpoint, capture the token, send it as
a bearer:

```yaml
pipeline:
  - operation: http.oauth2ClientCredentials
    with:
      tokenUrl: https://login.microsoftonline.com/${AZURE_TENANT_ID}/oauth2/v2.0/token
      clientId: ${API_CLIENT_ID}
      clientSecret: ${API_CLIENT_SECRET}
      scope: ${API_SCOPE}                 # api://<api-app-id-uri>/.default
    capture:
      - name: token
        from: json:$.access_token
  - operation: http.request
    with:
      url: ${API_BASE_URL}/me
      headers:
        Authorization: Bearer {{token}}
```

Every value is a `${ENV_VAR}` resolved at case-load time — from the environment,
falling back to this project's hosted secrets. No credential is ever in the case
file. Runnable version: `examples/cases/enterprise/example-entra-api-auth.yaml`.

For flag proof against an Entra-gated **flag API**, the same exchange is
available inside `flag_proof.control.auth` — see
[`docs/flag-proof.md`](flag-proof.md#flag-apis-behind-entra-id--org-oauth-controlauth).

### Your identity team owns these

- An **app registration** (service principal) for the test client.
- **Admin consent** for it.
- An **app-role assignment** granting it the API's role.

Qualify these blockers early — they can stop a pilot before it starts:

- Some orgs **prohibit non-interactive service principals** in non-production
  tenants. There is no engineering workaround.
- **Conditional Access on the token endpoint itself** (device compliance, a
  location gate) blocks the exchange. Only a self-hosted runner with an
  allowlisted egress IP helps — and only if your team will add it.

## UI-journey auth: use the app's test mode, not the IdP login

Do **not** script the interactive Entra login (password, MFA, Conditional Access
prompts) inside a browser journey — it is brittle and usually against policy.
Authenticate through the target app's own test mode instead, in priority order:

1. **The app's E2E mode** — the app server-mints the session when a signed test
   header or cookie is present. Seed it with the UI adapter's pre-navigation
   cookie step (`ui.setCookie`). Lowest fragility; your app team owns the hook.
2. **A reused `storageState` / cookie** captured from a real human login and
   refreshed on a schedule. Sessions expire — medium fragility.
3. **A dedicated test user** that is MFA-exempt with a Conditional Access
   exclusion for the runner's egress IP. Your identity team owns it.

If the app has no test mode yet, start the pilot with API + flag-proof cases
(no browser session needed) while the app team adds one; UI journeys land after.

## Conditional Access by IP

A **self-hosted runner has a stable egress IP** you can add to Entra
*trusted locations* / a named location, so a "block access from outside our
network" policy still lets the test client through. Cloud-hosted runners rotate
IPs and cannot be allowlisted — another reason to prefer a self-hosted runner
for an SSO-gated target.

## Not covered here

- **SSO into the ReleaseTwin dashboard** via your Entra tenant — that is about
  your humans viewing evidence, not about testing your app. Separate topic.
- A ReleaseTwin-operated tunnel/agent for cloud runners to reach isolated
  targets — out of scope; run a self-hosted runner instead.
