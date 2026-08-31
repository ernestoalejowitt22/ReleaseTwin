## Why

`docs/self-serve-funnel-plan.md` § B, and `README.md` line 225 ("deferred, not forgotten"): the
CLI decides which adapters to install **in code**. `CliRunner.RunCoreAsync` hardcodes "HTTP
always; Azure DevOps if its 5 env vars are present; LaunchDarkly if its 3; UI if
`RELEASETWIN_UI_ENABLED`." Adding an adapter means editing `CliRunner.cs`. For a self-serve user
writing their first case, there is no way to say "I only need HTTP" or "load the LaunchDarkly
adapter" without reading the source — the tool reads as a demo harness, not a product.

`releasetwin init` (sibling change `releasetwin-init-scaffold`) already emits a placeholder
`releasetwin.yaml`. This change gives that file meaning.

## What Changes

- **`releasetwin.yaml`** (project root, next to `cases/`) — an optional config file with an
  `adapters:` list naming which adapters to load:

  ```yaml
  adapters:
    - http            # credential-free, on by default even with no config
    - azure-devops
    - launchdarkly
    - ui
  ```

  Each adapter's **credentials** still come only from environment variables or the hosted
  `adapter-credentials` fetch — never from this file. The file names *which* adapters the project
  uses; it does not carry secrets.

- **Selection semantics:**
  - No `releasetwin.yaml`, or no `adapters:` key → **exactly today's behavior** (HTTP always;
    each credentialed adapter auto-loads if fully configured). Full backward compatibility.
  - `adapters:` present → only the listed adapters are considered. `http` is always available
    whether listed or not. A listed adapter whose credentials resolve → installed. A listed
    credentialed adapter with **no** credentials from either source → a clear startup error
    (you asked for it; it isn't configured), not a silent skip. An adapter that is configured in
    the environment but **not** listed → not installed (the list is authoritative when present).
  - Partial credential configuration is still the existing hard startup error, unchanged.

- **`CliRunner`** reads the file once at startup and drives composition from the resolved list
  instead of the inline `if (missing.Count == 0)` blocks. The per-adapter credential resolution
  (env → hosted fetch → precedence) is unchanged and simply invoked per selected adapter.

- **`ui` becomes selectable via config** in addition to `RELEASETWIN_UI_ENABLED` (the env var
  keeps working; either turns it on).

## Capabilities

### Modified Capabilities

- `cli-runner`: adapter selection is driven by an optional `releasetwin.yaml` `adapters:` list;
  absence preserves current auto-detection; a listed-but-unconfigured credentialed adapter is a
  startup error; the list is authoritative when present.

## Impact

- `src/ReleaseTwin.Cli/` — a small `ReleaseTwinConfig` loader (reuse the existing YAML dependency)
  + rework of the adapter-selection section of `RunCoreAsync` to iterate a resolved list.
- `src/ReleaseTwin.Cli/Scaffolding/` (from the sibling change) — the emitted `releasetwin.yaml`
  gains the real commented `adapters:` block.
- `tests/ReleaseTwin.Cli.Tests/` — no-config == today; `adapters: [http]` only; listed-but-
  unconfigured → startup error; env-configured-but-unlisted → not installed; `ui` via config.
- `docs/` — document `releasetwin.yaml` in the quickstart and a short reference.
- **No change** to `ReleaseTwin.Core`, the adapters themselves, `AdapterSdk`, or case-loading.
- `README.md` line 225 updated once this lands.

## Open Questions

- Filename: `releasetwin.yaml` vs `.releasetwin.yaml` vs `releasetwin.config.yaml`. Leaning
  `releasetwin.yaml` (visible, discoverable). Decide here; the sibling scaffold change follows.
- Should the file also allow a non-secret `flag_key` / `area_path` style parameter per adapter
  (things currently taken from env like `LAUNCHDARKLY_FLAG_KEY`, `AZDO_AREA_PATH`)? Proposed:
  **no** for the first version — keep the file to adapter *selection* only, revisit if users ask.
- Behavior when `releasetwin.yaml` is present but malformed: hard startup error (proposed) vs
  warn-and-fall-back-to-auto-detect. Proposed: hard error — a malformed config is a mistake.
