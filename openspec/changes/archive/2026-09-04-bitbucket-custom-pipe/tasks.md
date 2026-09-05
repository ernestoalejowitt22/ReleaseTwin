## 1. Wrapper image

- [x] 1.1 Create `integrations/bitbucket-pipe/Dockerfile`: `FROM` the published CLI
      image (pinned reference, matching the pattern `integrations/github-action/action.yml`
      already uses for its default), `COPY` in an entrypoint script, override
      `ENTRYPOINT` to run it.
- [x] 1.2 Create `integrations/bitbucket-pipe/entrypoint.sh`: `exec dotnet
      ReleaseTwin.Cli.dll "${CASES_PATH:-cases}"` — the one line of logic this
      wrapper exists for (see design.md - Decisions). `RELEASETWIN_JUNIT_XML`,
      `RELEASETWIN_SUMMARY_JSON`, and any adapter-credential env vars pass straight
      through unmodified; the script does not touch them.
- [x] 1.3 Copy `integrations/gitlab-component/LICENSE` (Apache-2.0) into
      `integrations/bitbucket-pipe/LICENSE`.

## 2. Pipe metadata

- [x] 2.1 Create `integrations/bitbucket-pipe/pipe.yml` declaring: the pipe name,
      the image reference (placeholder pinned digest, advanced by the release
      workflow — see Task 4), and variables `CASES_PATH` (default `cases`),
      `RELEASETWIN_JUNIT_XML` (default `test-results/junit.xml`, matching
      Bitbucket's own `**/test-results/*.xml` zero-config collection glob per
      design.md), and `RELEASETWIN_SUMMARY_JSON` (optional, no default).

## 3. Documentation

- [x] 3.1 Create `integrations/bitbucket-pipe/README.md`, mirroring
      `integrations/github-action/README.md` / `integrations/gitlab-component/README.md`'s
      shape: usage snippet, inputs table, no-ReleaseTwin-account statement,
      Apache-2.0 licensing note independent of the engine's copyleft license.
- [x] 3.2 Update `docs/ci.md`'s Bitbucket Pipelines section: add the `pipe:`-based
      snippet as the documented form, keep the existing raw `image:`/`script:`
      snippet as a labeled fallback.

## 4. Release wiring

- [x] 4.1 Add a step to `.github/workflows/release.yml`, after the existing CLI
      image build/push, that builds and pushes
      `integrations/bitbucket-pipe/Dockerfile` (tagged with the release version and
      `latest`, same registry/login already established in that job). Built with
      `--build-arg BASE_IMAGE=<name>@<digest>` from the CLI image just pushed, so
      it's never built from a stale base (design.md risk mitigation).
- [x] 4.2 Add a pinning step mirroring "Pin the Action default image to the new
      digest" (`release.yml:98-121`): `sed`-replace `pipe.yml`'s image reference
      and the Dockerfile's default `BASE_IMAGE` arg with the freshly built
      digest/version, commit, push — same gate (only reached after
      build+test+push succeed). Bundled into the existing pin step since both
      updates share the same commit/gate.

## 5. Verification

- [x] 5.1 Built the wrapper image locally (`docker build -t
      releasetwin-bitbucket-pipe:local integrations/bitbucket-pipe`) and ran it
      with no `CASES_PATH` set, mounting `examples/{cases,fixtures}` at
      `/app/{cases,fixtures}` (the base image's WORKDIR): resolved the default
      `cases` path, ran real cases, exited 1 reflecting genuine case outcomes (2
      passed, 2 failed — some demo cases need credentials not present in this
      smoke test, not a wrapper defect).
- [x] 5.2 Re-ran with `CASES_PATH=/app/other/cases-http-only` mounted from
      `examples/cases-http-only` — ran that directory instead, `PASS HTTP-DEMO-1`,
      exit 0.
- [x] 5.3 Set `RELEASETWIN_JUNIT_XML=/app/other/test-results/junit.xml` (passed
      straight through, untouched by the wrapper) — produced a real JUnit report
      at that path with the expected `<testcase name="HTTP-DEMO-1">`.
- [x] 5.4 `openspec validate bitbucket-custom-pipe --strict` passes.
