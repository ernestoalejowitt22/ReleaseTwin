## 1. Config loader

- [ ] 1.1 `src/ReleaseTwin.Cli/ReleaseTwinConfig.cs` — load `releasetwin.yaml` from the project
      root (the parent of the cases directory, or cwd). Model: `{ Adapters: string[]? }`.
      Absent file → `null`/empty. Malformed YAML or unknown adapter name → throw a clear
      `ReleaseTwinConfigException` the runner turns into a startup error + non-zero exit.
- [ ] 1.2 Known adapter names: `http`, `azure-devops`, `launchdarkly`, `ui`. Case-insensitive,
      trimmed.
- [ ] 1.3 Tests: absent, empty, `adapters: [http]`, unknown name, malformed YAML.

## 2. Drive composition from the resolved list

- [ ] 2.1 `CliRunner.RunCoreAsync` — compute the effective adapter set:
      - no config → today's behavior (consider all, auto-load each fully-configured one)
      - config present → only listed names; `http` always included
- [ ] 2.2 Per selected credentialed adapter, run the existing env → hosted-fetch → precedence
      resolution. If listed and unresolved → startup error naming the adapter (new). If not
      listed → skip entirely (do not even attempt the hosted fetch).
- [ ] 2.3 Partial-env-config startup error is unchanged (still fires regardless of the list).
- [ ] 2.4 `ui` selectable via config OR `RELEASETWIN_UI_ENABLED` (either enables it).
- [ ] 2.5 Keep the `azureDevOpsAdapter` / `launchDarklyAdapter` / `uiAdapter` locals and the
      `IFeatureStateControllerSource` discovery working off whatever ended up installed.

## 3. Scaffold + docs

- [ ] 3.1 Update the `releasetwin-init-scaffold` emitted `releasetwin.yaml` to the real commented
      `adapters:` block (coordinate with that change; whichever lands second wires it up).
- [ ] 3.2 `docs/` — a `releasetwin.yaml` reference section + a line in the quickstart.
- [ ] 3.3 `README.md` line 225 — drop "Config-driven adapter selection" from the deferred list.

## 4. Validation

- [ ] 4.1 `openspec validate config-driven-adapter-selection --strict` passes.
- [ ] 4.2 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green.
- [ ] 4.3 Manual: a project with `adapters: [http]` runs the HTTP example green; adding
      `- launchdarkly` with no LD env → startup error naming `launchdarkly`.

## Decisions to lock (from proposal Open Questions)

- [ ] D1 Filename: `releasetwin.yaml` (proposed).
- [ ] D2 First version is selection-only — no per-adapter non-secret params. (proposed)
- [ ] D3 Malformed config → hard startup error. (proposed)
