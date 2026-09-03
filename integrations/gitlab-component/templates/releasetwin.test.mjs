// SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
// SPDX-License-Identifier: Apache-2.0
//
// ci-report-formats: a dependency-free lint of the GitLab CI/CD Component template.
// GitLab's own CI lint needs a live instance; this asserts the structural invariants the
// spec relies on (JUnit report wired, tokenless, fail-closed, Apache-2.0 header).

import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const src = readFileSync(fileURLToPath(new URL("./releasetwin.yml", import.meta.url)), "utf8");

test("carries the Apache-2.0 SPDX header", () => {
  assert.match(src, /SPDX-License-Identifier: Apache-2\.0/);
});

test("declares the documented spec inputs", () => {
  assert.match(src, /^spec:/m);
  for (const input of ["cases-path", "image", "stage", "job-name"]) {
    assert.match(src, new RegExp(`^\\s{4}${input}:`, "m"), `missing input: ${input}`);
  }
});

test("clears the image entrypoint and invokes the CLI explicitly", () => {
  assert.match(src, /entrypoint:\s*\[""\]/);
  assert.match(src, /dotnet \/app\/ReleaseTwin\.Cli\.dll/);
});

test("requests the JUnit report and wires it into GitLab's native test surface", () => {
  assert.match(src, /--junit-xml junit\.xml/);
  assert.match(src, /reports:\s*\n\s*junit: junit\.xml/);
  assert.match(src, /when: always/);
});

test("references no GitLab API token (MR note is not in Phase 1)", () => {
  assert.doesNotMatch(src, /CI_JOB_TOKEN/);
  assert.doesNotMatch(src, /merge_requests\/.*\/notes/);
  assert.doesNotMatch(src, /PRIVATE-TOKEN|GITLAB_TOKEN/);
});

test("does not swallow a failing run", () => {
  // No `|| true`, no `allow_failure`, no `exit 0` — a non-zero CLI exit must fail the job.
  assert.doesNotMatch(src, /\|\|\s*true/);
  assert.doesNotMatch(src, /allow_failure:\s*true/);
});
