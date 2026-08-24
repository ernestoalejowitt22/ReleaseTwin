## Why

Once a customer signs up (`clerk-registration`) and creates a project, there's no way to say *what that project actually tests* beyond its name — no link to the GitHub repo, Bitbucket workspace, or Azure DevOps org it corresponds to. This is purely organizational metadata for the dashboard, deliberately scoped away from the much bigger question of whether the hosted platform should ever hold or act on a customer's third-party credentials (it explicitly should not — see design.md's Non-Goals and the project's standing "not a hosted execution runner" principle in `docs/installation-model.md`). Execution, adapters, and credentials all stay exactly where they are today: local, CLI-side, per `AZDO_*` env vars or case-file `${ENV_VAR}` interpolation. This change only lets a project be *labeled* with which external repo it corresponds to, chosen via a real OAuth-driven picker rather than a free-text field, so the label is guaranteed to name something that actually exists.

Scoped to GitHub only for this first slice — Bitbucket and Azure DevOps are real, named future work (own changes), not built speculatively here, matching this project's established one-provider-at-a-time pattern.

## What Changes

- A project gains an optional `Connection`: provider name (`"github"` for now), an external repo identifier (e.g. `owner/repo`), and when it was connected.
- The dashboard gains a "Connect GitHub" action per project. It runs a one-shot OAuth authorization-code exchange against GitHub — **not** the persistent web-session auth Clerk provides — lists the customer's repos, and lets them pick one. The resulting access token is used only within that single request cycle to call GitHub's API and is never persisted to any store (database, session, cookie, or log).
- The dashboard displays the connected repo (if any) next to the project's name.
- A customer can disconnect a project (delete the `Connection` row) through self-service, same as today's token revocation.

**Explicitly out of scope**:
- Bitbucket, Azure DevOps, or any other provider (future changes).
- Any use of the connected repo beyond display — no listing PRs, no webhooks, no triggering anything. It is a label, not an integration surface.
- Any credential custody: no GitHub token is ever stored, and no code path introduced here can read a customer's GitHub data after the initial picker request completes.
- Any change to execution, adapters, or the CLI.

## Capabilities

### New Capabilities
- `project-connections`: lets a signed-in customer label a project with an external GitHub repo via a real OAuth-driven picker, with no token custody and no execution/integration behavior beyond display.

### Modified Capabilities
(none)

## Impact

- New entity: `Connection` (or similar) — `ProjectId`, `Provider`, `ExternalRepo`, `ConnectedAt` — in `hosted/ReleaseTwin.Hosted.Api/Data/Entities/`.
- `hosted/ReleaseTwin.Hosted.Api/Data/HostedDbContext.cs`: new `DbSet`, foreign key to `Project`.
- New service/handler for the one-shot GitHub OAuth exchange — deliberately **not** built on ASP.NET Core's `AddOAuth`/remote-authentication-handler pipeline (that pipeline is designed to establish a signed-in session, not to run a single side-effecting request and discard its token — see design.md).
- `Dashboard.cshtml`/`Dashboard.cshtml.cs`: "Connect GitHub" / "Disconnect" UI, same org-scoping discipline as every other dashboard action.
- New external dependency: a GitHub OAuth App registered for *this* purpose — distinct from any OAuth app Clerk might itself be configured to offer as a login method inside Clerk's own dashboard. A one-time, manual, external setup step, same shape as the Clerk/GitHub-login precedents.
- No change to `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, any adapter, the CLI, or the ingest API's token-based auth.
