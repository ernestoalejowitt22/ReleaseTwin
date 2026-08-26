## 1. Manual setup (project owner)

- [x] 1.1 Register a second GitHub OAuth App: Homepage URL `http://localhost:3000`, Authorization
      callback URL `http://localhost:3000/connect/github/callback`. Generate a client secret.
- [x] 1.2 Create AWS Secrets Manager secret `releasetwin/e2e/github-account` with JSON value
      `{"username": "...", "password": "...", "totpSecret": "..."}` (the TOTP secret is the raw
      base32 string, not a generated code — the same value an authenticator app would be seeded
      with).
- [x] 1.3 Confirm the GitHub account (`ernestoalejowitt22`) has access to `NAHA` and that its
      current 2FA method is TOTP (this approach doesn't work for SMS/WebAuthn/passkey 2FA).
- [x] 1.4 Create AWS Secrets Manager secret `releasetwin/e2e/github-oauth-app` with JSON value
      `{"clientId": "...", "clientSecret": "...", "callbackUrl":
      "http://localhost:3000/connect/github/callback"}`, using the second OAuth App's real Client
      ID/Secret from task 1.1 (revised from the original plan to pass these as plain local env vars
      — see design.md's updated Decisions).

## 2. Local secret access

- [x] 2.1 Add `@aws-sdk/client-secrets-manager` and a TOTP library (e.g. `otplib`) to
      `web/package.json`.
- [x] 2.2 Add a `fetchGitHubTestAccount` task to `web/cypress.config.ts`'s `setupNodeEvents`: calls
      `GetSecretValueCommand` for `releasetwin/e2e/github-account` using the ambient AWS credential
      chain, parses the JSON, and returns `{ username, password, currentTotpCode }` (the TOTP code
      generated at call time, not cached).

## 3. Local OAuth App configuration

- [x] 3.1 Add `web/scripts/e2e-api-with-github.mjs`, which fetches
      `releasetwin/e2e/github-oauth-app` from AWS Secrets Manager and launches `dotnet run` with
      `GitHubConnection__ClientId`/`ClientSecret`/`CallbackUrl` set from it; wire it into
      `web/package.json` as `e2e:api:github`/`e2e:github` (separate from `e2e`/`e2e:api`, so other
      specs don't newly depend on this secret). Document `npm run e2e:github` in `web/README.md`.

## 4. Cypress spec

- [x] 4.1 New spec `web/cypress/e2e/github-connection.cy.ts`: signs in as the Clerk e2e test user
      (existing pattern), creates or reuses a project, clicks "Connect GitHub".
- [x] 4.2 Calls `fetchGitHubTestAccount`, then drives GitHub's real login form via
      `cy.origin('https://github.com', ...)`: username, password, and the TOTP code when prompted.
- [x] 4.3 Confirms the real consent screen ("Authorize ...") and completes it.
- [x] 4.4 Asserts the redirect lands back on `localhost:3000`'s callback page, `NAHA` appears in the
      real repo list returned by GitHub, selects it, and confirms the connection.
- [x] 4.5 Asserts the dashboard now shows `Connection — <project>` with `ernestoalejowitt22/NAHA`
      and provider `github`.
- [x] 4.6 Run the spec locally end to end; capture a screenshot of the confirmed connection.
