## 1. Subcommand dispatch

- [x] 1.1 `src/ReleaseTwin.Cli/Program.cs` — switch on `args[0]`: `init` / `new` → scaffolding,
      `run` → existing run path (optional dir arg + `--journey <id>@<v>`), anything else →
      today's fall-through (`--journey …` or bare dir / default). Add `--help` / `-h` usage text.
- [x] 1.2 Keep `Program.cs` dependency-free — a plain switch, no `System.CommandLine`.
- [x] 1.3 Tests: `run cases/` == `cases/`; `run --journey X@1` == `--journey X@1`; bare + no-args
      unchanged; `--help` lists `init`, `new`, `run`.

## 2. Scaffolding

- [x] 2.1 `src/ReleaseTwin.Cli/Scaffolding/Templates/case.yaml` + `fixture.json` as
      `EmbeddedResource`. Case = commented `http.request` + 2× `http.assertJsonPath` against a
      public test API, `{{caseId}}` placeholder, fixture `sha256` = real hash of `fixture.json`.
- [x] 2.2 `ScaffoldWriter` — `Init(dir)`: writes `cases/starter.yaml`, `fixtures/starter.json`,
      `releasetwin.yaml` (commented minimal starter), appends `.gitignore` lines. `New(dir, id)`:
      writes `cases/<id>.yaml` + `fixtures/<id>.json` with id substituted.
- [x] 2.3 No-clobber: `New` refuses if either target exists; `Init` refuses if `cases/` holds any
      `*.yaml`. On refusal — nothing written, one-line reason, non-zero exit. `.gitignore`
      append-only, dedup lines, create if absent.
- [x] 2.4 `--from-examples` — copy `/opt/releasetwin/examples/` recursively instead of the built-in
      starter; absent path → error pointing at plain `init`.
- [x] 2.5 Tests: `Init` writes the expected tree; recompute the fixture hash and assert it matches
      the template case's recorded hash (drift fails CI); `Init` then `run` on the scaffolded dir
      is green with no env; `Init` refuses on a dir with an existing case, writes nothing; `New`
      clobber guard; `.gitignore` idempotent append; `--from-examples` with the path present/absent.

## 3. Packaging

- [x] 3.1 `Dockerfile` — `COPY examples/ /opt/releasetwin/examples/` in the final stage, plus the
      **missing LaunchDarkly + Ui csproj/dir COPYs** (the CLI references both; restore was
      relying on them being reachable — now explicit). Added a repo `nuget.config` with
      `<clear/>` + nuget.org so a clone builds hermetically regardless of machine-level feeds.
- [x] 3.2 `docker build` succeeds; `docker run … init --from-examples` on a mounted dir copies
      the bundled tree and `… run` executes it. (Image-size delta: a few KB of YAML/JSON.)

## 4. Docs

- [x] 4.1 `docs/quickstart.md` — "Test your first API in 10 minutes": `docker run … init`, read
      the generated case, `docker run … run`, edit URL + assertions for your own API. Link from
      the top of `README.md`.

## 5. Validation

- [x] 5.1 `openspec validate releasetwin-init-scaffold --strict` passes.
- [x] 5.2 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green.
- [x] 5.3 Manual: `mkdir /tmp/rt-demo && cd /tmp/rt-demo && dotnet run --project …/ReleaseTwin.Cli -- init && dotnet run --project …/ReleaseTwin.Cli -- run` → green.

## 6. Sibling change (not here)

- [x] 6.1 `config-driven-adapter-selection` — `releasetwin.yaml` schema + adapter loading,
      replacing the hardcoded env-var blocks in `CliRunner.cs`. This change only emits a valid
      starter file; that one owns the semantics and the filename decision. DONE — merged in #37.
