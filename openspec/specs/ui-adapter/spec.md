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
