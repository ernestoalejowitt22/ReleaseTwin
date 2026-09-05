<!--
SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
SPDX-License-Identifier: Apache-2.0
-->

# Flag proof against GrowthBook (self-hosted)

A flag-proof case against a real, self-hosted [GrowthBook](https://www.growthbook.io/)
instance — no adapter, no plugin. The always-present HTTP adapter drives GrowthBook's own
REST API directly via `flag_proof.control`, exactly the mechanism documented in
[`docs/flag-proof.md`](../../docs/flag-proof.md#toggling-a-flag-no-adapter-knows-about-flag_proofcontrol).

## 1. Run GrowthBook locally

```bash
docker compose -f docker/docker-compose.yml up -d
```

(the compose file lives under `docker/` rather than this directory's root because
ReleaseTwin's case loader treats every `.yaml`/`.yml` file here as a case file)

This starts MongoDB and GrowthBook (app on `http://localhost:3000`, REST API on
`http://localhost:3100`).

## 2. One-time account setup

Unlike Unleash, GrowthBook has no environment-variable seed for its first account —
open `http://localhost:3000`, fill in a company name / your name / email / password, and
you land on its dashboard. Then create an API key: **Settings → API Keys → New Secret
Key**, description anything, role **Admin**, and reveal it.

## 3. Create the feature

```bash
curl -X POST http://localhost:3100/api/v1/features \
  -H "Authorization: Bearer <your-secret-key>" \
  -H 'Content-Type: application/json' \
  -d '{"id": "checkout-v2", "owner": "you", "valueType": "boolean", "defaultValue": "false"}'
```

(`owner` is required unless authenticating with a Personal Access Token instead of a
secret key.)

## 4. Run the case

```bash
GROWTHBOOK_URL=http://localhost:3100 \
GROWTHBOOK_API_KEY=<your-secret-key> \
dotnet run --project ../../src/ReleaseTwin.Cli -- examples/cases-flag-proof-growthbook
```

(or `releasetwin examples/cases-flag-proof-growthbook` if you installed the CLI as a
`dotnet tool` — see [`docs/install.md`](../../docs/install.md)).

Expect `FLAGPROOF FLAGPROOF-GROWTHBOOK-DEMO-1 (Passed)` — the known-bad leg
(`defaultValue: "false"`) fails the assertion, the known-good leg (`"true"`) passes it,
and `control.verify` confirms each toggle actually took before that leg runs.

## The recipe, if you already run GrowthBook

```yaml
flag_proof:
  feature_key: <your-flag-id>
  control:
    method: POST
    url: ${GROWTHBOOK_URL}/api/v1/features/{{featureKey}}
    headers:
      Authorization: "Bearer ${GROWTHBOOK_API_KEY}"
      Content-Type: application/json
    body: '{ "defaultValue": "{{enabled}}" }'
    verify:
      method: GET
      url: ${GROWTHBOOK_URL}/api/v1/features/{{featureKey}}
      headers:
        Authorization: "Bearer ${GROWTHBOOK_API_KEY}"
      json_path: $.feature.defaultValue
      expected: "{{enabled}}"
```

`defaultValue` is a JSON **string** (`"true"`/`"false"`), not a boolean — that's why
`{{enabled}}` sits inside quotes in the body rather than bare. `${GROWTHBOOK_API_KEY}`
resolves from the environment (or this project's hosted secrets) at case-load time —
never write a literal key in a case file, demo or not.

This recipe only covers a boolean flag's `defaultValue`. GrowthBook's targeting rules
(percentage rollouts, user attributes) aren't represented here — flag proof only needs
the simple on/off case to prove a fix, not the full rollout logic.
