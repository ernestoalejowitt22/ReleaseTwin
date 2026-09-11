## 1. Engineering overview doc

- [x] 1.1 Write `docs/engineering/overview.md`: CLI entry points, the `CaseExecutor` execution kernel's ordered pipeline, flag proof's paired-execution mechanic and outcome matrix, and each shipped adapter (Http, AzureDevOps, LaunchDarkly, Ui) — grounded in `src/`, cross-linking to the relevant `openspec/specs/*` for full behavior contracts rather than restating them.
- [x] 1.2 Confirm the doc says nothing about the private platform's internals (billing, hosted infra, credential storage) — this repo is public.

## 2. Wiki sync workflow

- [x] 2.1 Add `.github/workflows/docs-wiki-sync.yml`, copied from `releasetwin-platform`'s version and re-pointed at this repo — same trigger (`push` to `main`, `paths: ["docs/**"]`, plus `workflow_dispatch`), same `GITHUB_TOKEN`-authenticated clone/mirror/commit steps, same `Home.md` generation and self-link exclusion, same per-page "edit `docs/`, not here" notice.
- [x] 2.2 Enable the wiki feature via `gh api -X PATCH repos/ernestoalejowitt22/ReleaseTwin -f has_wiki=true` (confirmed necessary — `has_wiki` was `false` here too, checked directly via the API before writing this task).

## 3. One-time manual step (not automatable)

- [x] 3.1 **Needs the user to run this**: create this repo's wiki by saving one page through the GitHub UI, so `ReleaseTwin.wiki.git` exists before the workflow's first run.

## 4. Verification

- [x] 4.1 `openspec validate add-engineering-flow-docs --strict`.
- [x] 4.2 YAML-parse and `bash -n` each workflow step's script before pushing, same as was done for the platform repo's version.
- [x] 4.3 After merge and after task 3.1, confirm via a `workflow_dispatch` run and an actual `git clone` of the wiki repo that `docs/engineering/overview.md` and a real `Home.md` both land correctly — don't assume success from the workflow's green checkmark alone.
