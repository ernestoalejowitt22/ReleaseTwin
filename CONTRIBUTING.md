# Contributing to ReleaseTwin

Thanks for your interest. ReleaseTwin is early and solo-maintained, so a quick
issue before a large PR saves everyone time.

## Ground rules

- **Open an issue first** for anything beyond a small fix — use the
  [bug report or feature request form](https://github.com/ernestoalejowitt22/ReleaseTwin/issues/new/choose).
  See [`SUPPORT.md`](SUPPORT.md) for where non-code requests go. Large
  unsolicited PRs may be declined purely on scope.
- **One logical change per PR.** Keep diffs reviewable.
- **Match the surrounding code.** No reformatting-only churn, no dependency
  additions without discussion.

## Licensing of contributions

ReleaseTwin is open core — see [LICENSING.md](./LICENSING.md).

- Contributions to the engine (`src/`, `tests/`, repo-root build glue) are
  accepted under **AGPL-3.0** — see `LICENSE` and the Adapter Linking Exception
  in `LICENSE.EXCEPTIONS`.
- Contributions to `examples/` and `integrations/` are accepted under the
  **Apache License 2.0** (the scaffold copies examples into user projects; the
  Action is meant to be forked).

By submitting a pull request you certify the [Developer Certificate of
Origin](https://developercertificate.org/) (DCO) for your contribution — in
short, that you wrote it or have the right to submit it under these terms. Sign
off your commits with `git commit -s`.

## Development

### The engine (`src/`, `tests/`)

```bash
dotnet build ReleaseTwin.sln
dotnet test ReleaseTwin.sln
```

.NET 8 SDK, no other dependencies for the core and the HTTP adapter. The
bundled example runs with no credentials:

```bash
dotnet run --project src/ReleaseTwin.Cli -- examples/cases
```

## Specs and changes

Non-trivial work goes through OpenSpec (`openspec/`). Propose a change under
`openspec/changes/<name>/` (`openspec propose`), get it reviewed, then
implement against its tasks. Pure repo-governance or tooling changes may set
`skip_specs: true`.

## What not to include in a PR

- Secrets, tokens, `.env*` files, or real customer data — see
  [SECURITY.md](./SECURITY.md).
- Generated build output.
- Changes to `LICENSE`, `LICENSE.EXCEPTIONS`, `examples/LICENSE`,
  `integrations/github-action/LICENSE`, or `LICENSING.md` without prior agreement
  from the maintainer.
