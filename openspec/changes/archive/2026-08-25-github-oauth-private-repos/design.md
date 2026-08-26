## Context

See proposal.md for why. `GitHubConnectionFlowService.BuildAuthorizeUrl` currently builds the
authorize URL with `scope=read%3Auser`. Classic GitHub OAuth Apps (as opposed to GitHub Apps) have a
fixed, coarse-grained scope model: there is no scope that means "list private repositories without
being able to read their content" — `repo` is the only scope covering private repositories at all,
and it grants full read/write access to their content, issues, and pull requests.

## Goals / Non-Goals

**Goals:**
- Private repositories appear in the connection picker, matching how most real customers actually
  organize their work.
- The app's own behavior stays exactly as narrow as today — fetch the repo list once, store only
  the chosen identifier, never touch repository content, never persist the access token.

**Non-Goals:**
- Not migrating to a GitHub App (fine-grained, installation-based permissions with a genuine
  "Metadata: read-only" option that would avoid this scope-granularity problem entirely). That's a
  materially different integration model — a separate app registration flow, installation UX, and
  auth mechanism (JWT + per-installation tokens instead of a single OAuth token) — and a real
  option worth considering later if the breadth of the `repo` grant becomes a customer-facing
  concern, but out of scope for this change.
- Not changing what the app does with the token or the repository list — only what's requested.

## Decisions

**Request `read:user repo` scope, accept that it's broader than what the app uses.** Classic OAuth
Apps don't offer anything narrower for private-repo visibility (see Context). The alternative —
staying at `read:user` — makes the picker non-functional for the likely-common case of a customer's
real project being private, which is a worse outcome than requesting a broader-than-strictly-needed
grant that the app's own code disciplines itself not to exercise beyond listing. The updated
`project-connections` spec makes that discipline an explicit requirement ("A broader OAuth grant is
not exercised beyond listing repositories"), not just an implementation detail.

**No change to `ConnectionEndpoints` or the token-handling code itself.** The token is still used
only inside `ExchangeCodeForRepositoriesAsync`, still only to call `/user/repos`, still never
returned or persisted. Widening the scope changes what GitHub *allows* the token to do, not what
this app's code *does* with it.

## Risks / Trade-offs

- **Customers grant more than the app uses, and may not realize it.** GitHub's own consent screen
  is the only place this is disclosed (standard OAuth UX — this app doesn't currently add its own
  explanation of why `repo` is requested). → Accepted for now; revisit adding an explanatory note
  near the "Connect GitHub" button if this becomes a real customer question, or reconsider the
  GitHub App migration (see Non-Goals) if it becomes a recurring concern.
- **Already-connected customers get re-prompted for consent** the next time they start a connection
  flow, since GitHub notices the scope changed. This is GitHub's own standard behavior for any OAuth
  App scope change, not something this app needs to build — noted so it isn't mistaken for a bug
  when it happens.

## Migration Plan

None beyond the code change itself — no data migration, no new entity fields. Existing `Connection`
rows (which store only a repo identifier, not a token or scope) are unaffected. Deploys like any
other code change to `hosted/ReleaseTwin.Hosted.Api`.
