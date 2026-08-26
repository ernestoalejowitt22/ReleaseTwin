## Why

`GitHubConnectionFlowService` requests only the `read:user` OAuth scope, which limits GitHub's
`/user/repos` listing to a customer's *public* repositories — GitHub's classic OAuth Apps have no
narrower "list private repos" scope, so private-repo visibility requires the broader `repo` scope.
Most real customers' actual projects live in private repositories, not public ones, so the current
picker is close to useless for them in practice: they'd authorize, see an empty or irrelevant list,
and have no way to connect the repo they actually meant.

## What Changes

- `GitHubConnectionFlowService.BuildAuthorizeUrl` requests `read:user repo` instead of `read:user`,
  so a customer's private repositories appear in the picker.
- The app's own behavior does not change beyond that: it still only ever reads the repository list
  during the connection flow and stores nothing but the chosen repo's identifier — the existing
  "display metadata only" / "no repository content is ever fetched" guarantee is about what this
  app's code does, and stays true, even though the OAuth grant itself is now broader than that.
- Any customer who already connected under the old `read:user`-only scope will see GitHub's consent
  screen again the next time they start a connection, since GitHub re-prompts on a scope change —
  this is GitHub's own standard behavior, not something this app needs to implement.

## Capabilities

### Modified Capabilities
- `project-connections`: the repo picker's requirement now reflects that private repositories are
  included, and the existing "display metadata only" requirement is clarified to state explicitly
  that this holds regardless of the OAuth scope granted — it describes this app's own behavior, not
  a ceiling GitHub's grant enforces for it.

## Impact

- `hosted/ReleaseTwin.Hosted.Api/Services/GitHubConnectionFlowService.cs`: the `scope` query
  parameter in `BuildAuthorizeUrl`.
- Every real customer's "Connect GitHub" consent screen, immediately on deploy — they will see a
  request for private-repo access where they previously saw only public-profile access.
- The production GitHub OAuth App's own registered scope expectation (no change needed to the
  App's registration itself — scope is requested per-authorization, not configured on the App).
