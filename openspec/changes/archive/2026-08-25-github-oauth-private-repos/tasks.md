## 1. Widen the requested scope

- [x] 1.1 In `GitHubConnectionFlowService.BuildAuthorizeUrl`, change the requested scope from
      `read:user` to `read:user repo`.
- [x] 1.2 Update the existing `Assert.Contains("scope=read%3Auser", ...)` assertion in
      `ConnectionFlowTests.cs` to reflect the new scope string, and add a test asserting the URL
      also requests `repo`.

## 2. Verify

- [x] 2.1 Run the full `.NET` test suite to confirm nothing else asserts on the old scope value.
- [x] 2.2 Re-run `github-connection.cy.ts` (e2e-github-connection-flow) end to end and confirm
      `ernestoalejowitt22/NAHA` now appears in the real picker and can be connected — this is the
      real-world confirmation that private repos are now visible.
