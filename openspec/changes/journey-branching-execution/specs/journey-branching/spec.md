## ADDED Requirements

### Requirement: A pipeline entry is either a step or a choice
A case's pipeline SHALL be an ordered list of entries, each either a step or a choice
(`kind: choice` with `when`, `then`, `else`). A pipeline containing only steps SHALL load and execute
exactly as it did before choices existed.

#### Scenario: A linear case is unchanged
- **WHEN** a case's pipeline contains only step entries with no `when` guard
- **THEN** it loads, executes, and records evidence exactly as before this capability existed

#### Scenario: A choice runs only its taken branch
- **WHEN** a choice's condition evaluates true
- **THEN** the steps of its `then` branch execute in order, the steps of its `else` branch do not
  execute, and execution continues with the entry after the choice

#### Scenario: An unknown entry kind is rejected at load time
- **WHEN** a pipeline entry declares a `kind` other than `choice`
- **THEN** the loader rejects the case file with an error naming the unknown kind

### Requirement: A branch holds plain steps only
A choice's `then` and `else` SHALL contain only steps. A choice nested inside a branch SHALL be
rejected at load time and SHALL be unrepresentable in the core model.

#### Scenario: A nested choice is rejected
- **WHEN** a case file contains a `kind: choice` entry inside another choice's `then` or `else`
- **THEN** the loader rejects the case file with an error stating choices cannot be nested

### Requirement: Conditions use a fixed operator set over captured values
A condition SHALL name a capture (`ref`, bare or as `{{name}}`), an operator (`==`, `!=`, `exists`,
`!exists`), and for `==`/`!=` a `value`. Comparison SHALL be ordinal string equality. A capture that
is absent at evaluation time SHALL make `==` and `exists` false and `!=` and `!exists` true.

#### Scenario: An unsupported operator is rejected
- **WHEN** a condition declares an operator outside the supported set
- **THEN** the loader rejects the case file naming the unsupported operator

#### Scenario: An equality condition without a value is rejected
- **WHEN** a condition uses `==` or `!=` and declares no `value`
- **THEN** the loader rejects the case file

#### Scenario: An absent capture does not fail the case
- **WHEN** a condition's capture was never produced in this run
- **THEN** the condition evaluates per the absent-capture rule and the run continues

### Requirement: Untaken branches and guarded-off steps record NotExecuted at stable positions
Every step of a pipeline SHALL have one evidence record per run, at an index equal to its position
in the pipeline flattened in declaration order (a choice contributing its `then` steps followed by
its `else` steps). Steps of the untaken branch, guarded-off steps, and steps after a halting
failure SHALL record `NotExecuted`.

#### Scenario: The untaken branch records NotExecuted
- **WHEN** a run takes a choice's `then` branch with evidence capture enabled
- **THEN** every `else` step appears in the evidence with outcome `NotExecuted`

#### Scenario: Opposite branches produce the same step positions
- **WHEN** two runs of the same case take opposite branches of a choice
- **THEN** both runs' evidence lists the same indices and operation names, differing only in outcomes

#### Scenario: A failure inside a branch halts the pipeline
- **WHEN** a step inside the taken branch fails without being declared as an expected failure
- **THEN** the case fails with that step's classification and every later step records `NotExecuted`

### Requirement: A step may carry a when guard
A step MAY declare a `when` condition. When it evaluates false, the step SHALL NOT execute, SHALL
record `NotExecuted`, and the run SHALL continue with the next entry.

#### Scenario: A guarded-off step is skipped and the run continues
- **WHEN** a step's `when` condition evaluates false
- **THEN** the step's operation is not invoked, it records `NotExecuted`, and later entries execute

#### Scenario: A guarded-on step runs normally
- **WHEN** a step's `when` condition evaluates true
- **THEN** the step executes and records its outcome as if it had no guard

### Requirement: Capture references resolve within static scope
A condition's `ref` SHALL be captured by a step earlier in the same branch or earlier than the
enclosing choice. A `{{name}}` parameter reference to a name that is captured in the case only
inside a branch not enclosing the reference SHALL be rejected at load time as not in scope.

#### Scenario: A capture from the sibling branch is not in scope
- **WHEN** a step in a choice's `else` branch references a name captured only in its `then` branch
- **THEN** the loader rejects the case file stating the capture is not in scope

#### Scenario: A branch capture is not in scope after the join
- **WHEN** a step after a choice references, or guards on, a name captured only inside that choice
- **THEN** the loader rejects the case file stating the capture is not in scope

#### Scenario: A capture from before the choice is in scope in either branch
- **WHEN** a step in either branch references a name captured before the choice
- **THEN** the case file loads
