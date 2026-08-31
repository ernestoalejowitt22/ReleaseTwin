# Credential / access preflight — landing-demo-ci-loop

Ran 2026-08-30. Covers the GitHub, GHCR, AWS, and LaunchDarkly surface the change touches.

## Status table

| Item | How to verify now | Status / gap |
|---|---|---|
| `gh` auth, `workflow` scope | `gh auth status` | ✓ `ernestoalejowitt22`, scopes `gist, read:org, repo, workflow` |
| `gh` can reach NAHA repo | `gh repo view ernestoalejowitt22/NAHA` | ✓ `repo` scope covers the private repo (same owner) |
| `gh` `read:packages` (verify GHCR visibility via API) | `gh api "/users/ernestoalejowitt22/packages/container/releasetwin%2Fcli"` | ✗ scope missing → `gh auth refresh -s read:packages` (only needed to *inspect* the package; not needed to publish) |
| CLI image `ghcr.io/ernestoalejowitt22/releasetwin/cli` exists | `git tag -l 'v*'` / GHCR UI | ✗ **no `v*.*.*` tag ever cut** → `release.yml` has never run → image does not exist. Blocks everything downstream. |
| `release.yml` can push to GHCR | inspect `.github/workflows/release.yml` | ✓ uses `secrets.GITHUB_TOKEN` + `permissions: packages: write` — no extra secret |
| GHCR package reachable from NAHA CI | after publish: `docker pull …:latest` anon; or NAHA workflow `docker login ghcr.io` | ✗ pending publish. User-owned package is private by default; NAHA's `GITHUB_TOKEN` cannot pull a package linked to the ReleaseTwin repo unless the package is made **public** or NAHA is added under the package's *Manage Actions access*. |
| AWS Secrets Manager read | `aws sts get-caller-identity` | ✓ IAM user `releasetwin-e2e-secrets-reader`, account `846136340491` |
| `releasetwin/e2e/naha-account` | `aws secretsmanager get-secret-value --secret-id releasetwin/e2e/naha-account` | ✓ keys: `adminEmail`, `apiBaseUrl`, `e2eAuthSecret`, `adminUiBaseUrl`, `roleCookieName`, `roleCookieValue` |
| `releasetwin/e2e/launchdarkly-account` | `aws secretsmanager get-secret-value --secret-id releasetwin/e2e/launchdarkly-account` | ✓ keys: `apiToken`, `environmentKey`, `projectKey` |
| NAHA repo Actions secrets for the RT gate | `gh secret list -R ernestoalejowitt22/NAHA` | ✗ NAHA has `ANTHROPIC_API_KEY`, `NAHA_TESTING_READ_TOKEN`, `TESTING_AUTHOR_DISPATCH_TOKEN` only. **No NAHA e2e API URL / secret / admin email.** Must add: `RELEASETWIN_NAHA_API_URL`, `RELEASETWIN_NAHA_E2E_SECRET`, `RELEASETWIN_NAHA_ADMIN_EMAIL` (values from the AWS secret above). |
| Docker daemon (local) | `docker ps` | ✗ not running locally — not required; the image runs in CI, not on this machine |

## Gaps the user must close (in order)

1. **Publish the CLI image.** `git tag v0.1.0 && git push origin v0.1.0`, confirm `release.yml`
   pushes `ghcr.io/ernestoalejowitt22/releasetwin/cli:{0.1.0,latest}`.
2. **Make it pullable from NAHA.** Set the GHCR package public, OR add `ernestoalejowitt22/NAHA`
   under the package's *Manage Actions access*. If kept private, the NAHA workflow also needs a
   `docker/login-action` step with `GITHUB_TOKEN`.
3. **Add NAHA repo secrets** (values from `releasetwin/e2e/naha-account`):
   ```
   gh secret set RELEASETWIN_NAHA_API_URL     -R ernestoalejowitt22/NAHA   # <- apiBaseUrl
   gh secret set RELEASETWIN_NAHA_E2E_SECRET  -R ernestoalejowitt22/NAHA   # <- e2eAuthSecret
   gh secret set RELEASETWIN_NAHA_ADMIN_EMAIL -R ernestoalejowitt22/NAHA   # <- adminEmail
   ```
4. `gh auth refresh -s read:packages` (optional — only to script the visibility check).

## Notes

- The NAHA e2e login endpoint (`/v1/e2e/login`) returns 404 unless that deployment sets
  `E2E_AUTH_ENABLED=true`. Confirm the target deployment (`apiBaseUrl` in the secret) has it on
  before wiring the passing case, or the gate goes red on every NAHA PR.
- The capture script (task 3.x) reads both AWS secrets directly — no new credentials, same path
  the video demo already uses.
