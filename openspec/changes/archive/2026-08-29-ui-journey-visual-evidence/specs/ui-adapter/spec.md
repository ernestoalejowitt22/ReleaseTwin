## ADDED Requirements

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
