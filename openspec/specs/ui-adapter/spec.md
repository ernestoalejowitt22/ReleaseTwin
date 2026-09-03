# ui-adapter Specification

## Purpose

Lets a case drive a real browser as one leg of a journey — the UI half of flows like "log in through
the UI, then verify the effect through the API" — using the same declarative case-file and pipeline
model every other adapter already uses, so a journey spanning UI and API legs is one case, not a
separate tool.
## Requirements
### Requirement: A case can declare UI operations as pipeline steps
A case's pipeline MAY include browser-driven steps (at minimum: navigate, click, fill a field, wait
for a condition, assert something is visible) alongside operations from any other installed adapter.
The adapter SHALL execute them in the same ordered pipeline as any other operation.

#### Scenario: A UI step and an API step run in the same case
- **WHEN** a case's pipeline includes both a UI step and an HTTP-adapter step
- **THEN** both execute in declared order as part of the same case run, under the same core-execution
  reporting and cleanup guarantees every other case already has

### Requirement: UI steps integrate with existing failure classification and cleanup
A failing UI step SHALL be classified and reported the same way any other operation's failure is,
and cleanup SHALL still run regardless of whether a UI step passed or failed.

#### Scenario: A failed UI step still runs cleanup
- **WHEN** a UI step fails partway through a case's pipeline
- **THEN** the case's declared cleanup still runs, and the failure is classified consistently with
  how a non-UI operation's failure would be

### Requirement: A value observed in the UI can be captured like any other step's result
A UI step MAY declare a capture (e.g. text content of an element) usable by later steps. The adapter
SHALL populate it via the same mechanism `value-capture` defines for any other adapter's steps.

#### Scenario: Text captured from the UI is used in a later API step
- **WHEN** a UI step captures a value visible on the page (e.g. a confirmation number)
- **THEN** a later API step in the same case can reference that captured value

### Requirement: A case can seed a browser cookie before navigation
A case's pipeline MAY include a step that sets a cookie on the run's browser session before a later navigation step, so a journey can drive an app that gates access on a cookie (an E2E auth bypass, a feature toggle, a locale) entirely from case-file data. The step SHALL accept a cookie name and value and a scope (a URL, or a domain plus path), and MAY accept the standard cookie attributes (secure, httpOnly, sameSite, expiry). It participates in the same ordered pipeline, failure classification, capture mechanism, and cleanup as any other UI step.

#### Scenario: A seeded cookie authenticates a later navigation
- **WHEN** a case's pipeline sets a cookie, then navigates to a URL whose app grants access based on that cookie
- **THEN** the navigation loads the authenticated view, in the same run, without any separate login step

#### Scenario: A cookie set on one step is visible to every later step in the run
- **WHEN** a case sets a cookie in one step and performs UI actions in later steps against the same site
- **THEN** those later steps see the cookie, because all UI steps in a case run share one browser session

#### Scenario: An invalid cookie declaration fails the step, not the process
- **WHEN** a cookie step is missing a required field (name, value, or scope) or names a malformed scope
- **THEN** that step fails and is classified like any other operation failure, the case's cleanup still runs, and the CLI does not crash

### Requirement: A malformed or unsupported cookie scope is a clear failure
The cookie step SHALL require exactly one valid scope form — a full URL, or a domain (with optional path) — and SHALL reject a declaration that supplies neither or both, or a URL that is not absolute, as a step failure with a message naming the problem.

#### Scenario: Neither url nor domain is supplied
- **WHEN** a cookie step declares a name and value but no `url` and no `domain`
- **THEN** the step fails with a message stating that a `url` or a `domain` is required

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

