## Why

`docs/self-serve-funnel-plan.md` § B: "get started in 10 minutes without cloning." Today, writing
your first case means cloning this repo for `examples/`, then hand-building a `cases/` +
`fixtures/` layout and getting the fixture `sha256`, the `oracle.locator`, and the
`${VAR}` interpolation syntax right from documentation alone. That is rung-1 friction the funnel
can't afford — the whole pitch is "run `docker run` against your own API, then write your own
case."

The CLI has no subcommands at all — `Program.cs` treats `args[0]` as either `--journey` or a
cases directory. There is nowhere for a scaffold command to live.

## What Changes

- **New `releasetwin init` command** — scaffolds a working project in the current directory:
  - `cases/` with one starter case: a real `http.request` + `http.assertJsonPath` against a
    public test API, heavily commented, that passes with **zero configuration or credentials**
    (the same shape as `examples/cases/example-http.yaml`).
  - `fixtures/` with the matching fixture JSON and the correct `sha256` already filled in.
  - `releasetwin.yaml` — a minimal, commented config file (its schema is owned by the sibling
    `config-driven-adapter-selection` change; `init` only emits a valid starter).
  - `.gitignore` entries for local run output.
  - Refuses to overwrite: if `cases/` already has `.yaml` files, it stops with a clear message
    and writes nothing.
- **New `releasetwin new <case-id>` command** — adds one more case + fixture pair to an existing
  project, using the same starter template with the given id. Same no-overwrite guard per file.
- **`Program.cs` gains real subcommand dispatch** — `init`, `new`, `run` (the current default,
  still the behavior when no subcommand is given, for compatibility), and `--journey` folded in
  as `run --journey`. `releasetwin --help` lists them.
- **The Docker image bundles `examples/`** at a known path (`/opt/releasetwin/examples`) and
  `releasetwin init --from-examples` copies from there, so image users get the full example set
  without the repo. Plain `init` still works offline from the built-in template.

## Capabilities

### New Capabilities

- `case-scaffolding`: the `init` / `new` commands — what they write, the no-clobber guarantee,
  the zero-config starter case, and that a freshly `init`-ed project runs green immediately.

### Modified Capabilities

- `cli-runner`: the CLI dispatches subcommands (`init`, `new`, `run`); the no-subcommand and
  `--journey` invocations keep their current behavior.
- `cli-packaging`: the container image includes the `examples/` tree at a documented path.

## Impact

- `src/ReleaseTwin.Cli/Program.cs` — subcommand parsing (small; no framework, keep it a switch).
- New `src/ReleaseTwin.Cli/Scaffolding/` — the template writer + embedded template resources.
- `Dockerfile` — `COPY examples/ /opt/releasetwin/examples/`.
- `tests/` — scaffolding tests (writes expected files, sha256 correct, no-clobber, `init`-then-
  `run` is green).
- `docs/` — a standalone "Test your first API in 10 minutes" quickstart (funnel plan § B).
- **No change** to `ReleaseTwin.Core`, the adapters, case-loading, or the pipeline. `init` is a
  file-writer; `run` is unchanged.

## Not in scope

- The `releasetwin.yaml` **schema and adapter-selection semantics** — that is the sibling change
  `config-driven-adapter-selection` (funnel plan § B, replaces the hardcoded env-var logic in
  `CliRunner.cs`). This change only emits a valid starter file.
- NuGet / `dotnet tool` packaging (funnel plan § C).
- Publishing the quickstart or a terminal-recording asset (the landing SVG already exists).
