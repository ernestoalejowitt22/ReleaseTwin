## Context

`src/ReleaseTwin.Cli/Program.cs` is ~22 lines: build the environment dict, then either
`--journey <id>@<v>` → `RunJourneyAsync`, or `args[0]` (default `"cases"`) → `RunAsync`. There is
no subcommand layer and no dependency like `System.CommandLine`.

`examples/cases/example-http.yaml` is the canonical zero-config case: a real GET against
`jsonplaceholder.typicode.com` plus two `http.assertJsonPath` assertions, with a fixture that
carries a `sha256`. `case-loading` verifies the fixture hash, so a scaffolded fixture must ship
with the right hash baked in.

## Goals / Non-Goals

**Goals**
- `mkdir demo && cd demo && releasetwin init && releasetwin run` → a green run, no credentials,
  no repo clone.
- Never destroy existing work — every write is guarded.
- Keep `Program.cs` dependency-free (a hand-rolled switch, not a CLI framework).

**Non-Goals**
- A plugin/adapter config format (sibling change).
- Interactive prompts / TTY UI. `init` is non-interactive and deterministic.
- Templating engines. The starter files are static resources with a single `{{caseId}}`
  substitution.

## Decisions

### D1: Subcommand dispatch is a switch on `args[0]`
`init`, `new`, `run` are matched literally; anything else (including a bare directory path or
`--journey …`) falls through to today's behavior so existing invocations and the Docker `CMD`
keep working. `run` accepts `--journey <id>@<v>` and an optional directory. `--help` / `-h` /
no-args-in-a-non-project-dir prints usage.
- *Alternative rejected:* `System.CommandLine`. A dependency and a restructure for four verbs.

### D2: Starter files are embedded resources, copied verbatim with one substitution
`Scaffolding/Templates/` holds `case.yaml` and `fixture.json` as `EmbeddedResource`. `init`
writes `cases/starter.yaml` + `fixtures/starter.json` + `releasetwin.yaml` + appends to
`.gitignore`. `new <id>` writes `cases/<id>.yaml` + `fixtures/<id>.json`. The only substitution
is `{{caseId}}` in the case file. The fixture's `sha256` in the template case is the real hash
of the template fixture — a scaffolding test recomputes it so drift fails CI.
- *Alternative rejected:* compute the fixture hash at write time. Works, but then the template
  case file can't be a static resource and the "these are the exact bytes you'd hand-write"
  property is lost.

### D3: No-clobber is per-target-file, `init` is also per-project
`new` refuses if `cases/<id>.yaml` or `fixtures/<id>.json` exists. `init` refuses outright if
`cases/` already contains any `*.yaml` (treat the project as already initialized). On refusal:
non-zero exit, a one-line reason, nothing written. `.gitignore` is appended to (create if
absent), never rewritten, and only if the lines aren't already present.

### D4: `examples/` in the image lives at `/opt/releasetwin/examples`
`Dockerfile` `COPY examples/ /opt/releasetwin/examples/` in the final stage. `init --from-examples`
copies that whole tree instead of the built-in single starter; if the path is absent (running
from source, not the image) it errors telling you to use plain `init` or clone. The built-in
single-starter path has no filesystem dependency and is the documented default.

### D5: Quickstart doc is standalone
`docs/quickstart.md` — "Test your first API in 10 minutes": `docker run … init`, look at the
generated case, `docker run … run`, then edit the URL/assertions to hit your own API. Linked from
the README's top, not buried.

## Risks / Trade-offs

- **`jsonplaceholder.typicode.com` is down when a newcomer runs the starter** → the case fails on
  a network error, which reads as "the tool is broken" on first contact. Mitigation: the starter
  case comment says it hits a public API and how to point it at something local; consider a
  second fully-offline starter later (needs a loopback fixture the HTTP adapter can serve — out
  of scope here).
- **`Program.cs` switch grows unreadable as verbs are added** → revisit `System.CommandLine` if a
  fourth or fifth verb lands; three is fine as a switch.
- **Docker image size from bundling `examples/`** → the tree is a few KB of YAML/JSON; negligible.

## Migration Plan

Additive. No existing invocation changes: `releasetwin cases/`, `releasetwin` (default dir),
`releasetwin --journey …`, and the Docker `CMD ["/workspace/cases"]` all dispatch to the same
code as today. Ship the command + the quickstart together. Rollback is a straight revert — no
persisted state, no schema.

## Open Questions

- Should `init` also drop a starter GitHub Actions workflow (`.github/workflows/releasetwin.yml`)?
  Leaning yes but it overlaps funnel plan § C (the Action wrapper) — deferring until C is scoped.
- `releasetwin.yaml` filename vs `.releasetwin.yaml` vs `releasetwin.toml` — the sibling change
  owns this; `init` follows whatever it picks.
