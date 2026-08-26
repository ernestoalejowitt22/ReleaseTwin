## Purpose

Lets a step in a case's pipeline capture a value from its own result and make it available, by
name, to later steps in the same run — regardless of which adapter produced or will consume it —
so multi-step flows (login-then-call, create-then-reference, a UI leg feeding an API leg) can be
expressed as one case instead of requiring values to be known in advance.

## ADDED Requirements

### Requirement: A step can declare a named capture from its own result
A pipeline step MAY declare one or more captures, each naming a value drawn from that step's own
result (e.g. a JSON field via a path expression, a response header, a cookie) and binding it to a
name unique within that case run. When declared, the runner SHALL populate each named capture from
that step's own result once the step completes.

#### Scenario: A captured JSON field is available after the step runs
- **WHEN** a step declares a capture of a JSON field from its result and that field is present
- **THEN** the named capture holds that field's value once the step completes

### Requirement: A later step can reference a captured value
A pipeline step's parameters MAY reference a name captured by an earlier step in the same case run;
the reference SHALL resolve to the captured value when that step executes.

#### Scenario: A later step receives the captured value
- **WHEN** a step's parameters reference a name captured by an earlier step in the same run
- **THEN** the operation receives the captured value in place of the reference when it executes

### Requirement: Referencing an unavailable capture is a reported error, not silent
If a step's parameters reference a name that was never captured by an earlier step in the same run
(not declared, or declared by a step that hasn't executed yet, or that failed before capturing),
the case SHALL fail with an error identifying the missing capture, not proceed with a blank or
literal placeholder value.

#### Scenario: Referencing a name no earlier step captured
- **WHEN** a step's parameters reference a name that no earlier step in the run has captured
- **THEN** the case fails with an error naming the missing capture, and the step is not executed
  with a placeholder value

### Requirement: Captured values do not persist beyond a single case run
A value captured during one case's execution SHALL NOT be visible to any other case's execution,
including a later run of the same case file.

#### Scenario: A capture from one case is invisible to another
- **WHEN** two different cases run in the same CLI invocation, and one captures a value
- **THEN** the other case cannot reference that captured name, even if it uses the same name itself
