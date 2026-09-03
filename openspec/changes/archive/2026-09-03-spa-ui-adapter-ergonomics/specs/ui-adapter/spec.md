## ADDED Requirements

### Requirement: A case can assert an element's text content

A case's pipeline MAY include a step that asserts the text content of an element
identified by a selector. The step SHALL support an exact-match mode and a
contains mode, and SHALL apply the same `${VAR}` (load-time) and `{{capture}}`
(per-run) substitution to the expected value that other operations use. The step
participates in the ordered pipeline, capture mechanism, failure classification,
and cleanup exactly as any other UI operation. `ui.assertVisible` is unchanged
and still available for presence-only checks.

#### Scenario: Exact text assertion passes

- **WHEN** a case asserts that the text of an element equals a given string, and
  the rendered element's trimmed text is that string
- **THEN** the step passes and the pipeline continues

#### Scenario: Contains assertion against dynamic text

- **WHEN** a case asserts that an element's text contains an expected substring
  built from an earlier `{{capture}}`
- **THEN** the substitution is applied before the comparison, and the step passes
  only if the rendered text contains the resolved substring

#### Scenario: Text mismatch is a classified step failure

- **WHEN** the element exists but its text does not satisfy the assertion
- **THEN** the step fails with a message naming the selector, the expected value,
  and the actual text; the case's cleanup still runs; the failure is classified
  the same way any other operation failure is

#### Scenario: Missing element is a step failure, not a crash

- **WHEN** the selector matches no element within the step's timeout
- **THEN** the step fails and is classified like any other operation failure, and
  the CLI process does not crash

### Requirement: A wait step can wait for a client-side URL change

A `ui.waitFor` step MAY wait for the browser's current URL to match an expected
value instead of waiting for a selector state, so a case can synchronize on a
single-page-app route transition that does not trigger a full page load. The
match SHALL support at least a substring or glob form against the live URL. A
single `ui.waitFor` step declares either a selector wait or a URL wait, not both.
The step honors the same timeout handling and failure classification as the
existing selector wait.

#### Scenario: Wait resolves after an in-app route change

- **WHEN** a case clicks a control that changes the SPA route client-side, then
  waits for the URL to match the new route
- **THEN** the wait resolves once the URL matches, and later steps run against the
  new view

#### Scenario: URL never matches within the timeout

- **WHEN** the URL does not match the expected value before the step's timeout
- **THEN** the step fails with a message naming the expected match and the last
  observed URL, and the failure is classified like any other operation failure

#### Scenario: A wait step declaring both a selector and a URL match is rejected

- **WHEN** a `ui.waitFor` step declares both a selector and a URL match
- **THEN** the step fails with a message stating that exactly one wait target is
  allowed, and the case's cleanup still runs
