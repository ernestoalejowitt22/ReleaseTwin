---
name: openspec-status-sweep
description: Status sweep of all open OpenSpec work before picking the next thing. Use at the start of a session or when the user asks "what's next", "status", or which change to pick up.
allowed-tools: Bash(openspec:*), Bash(ls:*), Bash(grep:*), Bash(git:*)
metadata:
  author: ernestoalejo
  version: "1.0"
---

Give the user a decision-ready picture of every open workstream, then recommend one.

**Steps**

1. List non-archived changes: `ls openspec/changes/` (ignore `archive/`).
2. For each change, read `proposal.md` and `tasks.md`. Count `- [x]` vs `- [ ]`.
   Capture the text of every unchecked task.
3. Classify each incomplete task as:
   - **blocked on user** — needs AWS/infra access, a credential, a console click,
     or an external merge (look for "Needs the user to run this" markers).
   - **blocked on infra** — waiting on a deploy, a merge in another repo, an env
     toggle.
   - **ready to go** — Claude can do it now.
4. Scan for clarification questions the user never answered (search proposals/design
   docs and recent conversation for open questions).
5. Check `git status` and `git log --oneline -5` for uncommitted or unpushed work.
6. Report as a table: change | tasks done/total | blocked-on | one-line next action.
   Then recommend ONE change to pick up and say why. Do not start work — wait for
   the user to choose.
