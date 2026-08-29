# evidence-capture Specification

## Purpose
Defines what structured evidence a case or flag-proof run may collect from any adapter, and the redaction that runs inside the customer's own CLI before any of it is uploaded — so raw request/response content and secrets never leave the customer's infrastructure.
## Requirements
### Requirement: Evidence capture is opt-in and off by default
A run SHALL collect structured evidence only when evidence capture is explicitly enabled for that run. When capture is not enabled, execution, the case report, and any upload SHALL be identical to the behavior before this capability existed.

#### Scenario: Capture disabled leaves the run unchanged
- **WHEN** a case is run without evidence capture enabled
- **THEN** no evidence document is produced, and the case report and its upload are byte-for-byte what they would be without this capability

#### Scenario: Capture enabled produces an evidence document
- **WHEN** a case is run with evidence capture enabled
- **THEN** the run produces a structured evidence document alongside its report

### Requirement: A run evidence document is an ordered per-step record
When capture is enabled, the evidence document SHALL contain the case identifier and oracle locator, and an ordered list of executed steps. Each step entry SHALL record the operation name, its outcome (pass / fail / expected-failure / timeout), its duration, any adapter-emitted evidence for that step, and assertion detail (the checked path or expression, the expected value, and the observed value) where the operation is an assertion.

#### Scenario: Step order and outcomes are preserved
- **WHEN** a case with three pipeline steps is run with capture enabled and the second step fails
- **THEN** the evidence document lists all three steps in pipeline order with the second marked failed and the third marked as not executed

#### Scenario: Assertion detail is captured
- **WHEN** an assertion step fails because an observed value differs from the expected value
- **THEN** that step's evidence entry records the checked path, the expected value, and the observed value

### Requirement: Flag-proof runs capture evidence for each leg
When capture is enabled for a case in flag-proof mode, the evidence document SHALL record a separate per-step record for the known-bad leg and the known-good leg, each identified as such.

#### Scenario: Both legs are represented
- **WHEN** a flag-proof case is run with capture enabled
- **THEN** the evidence document contains a distinct step record for the known-bad leg and for the known-good leg

### Requirement: Redaction runs in the CLI before upload
All redaction of captured evidence SHALL be performed by the CLI, on the machine that ran the case, before any evidence is transmitted. Un-redacted evidence SHALL NOT be transmitted to any hosted endpoint under any configuration.

#### Scenario: Only redacted evidence is uploaded
- **WHEN** the CLI uploads a run's evidence
- **THEN** the payload contains only the post-redaction evidence document, and no code path uploads the pre-redaction form

### Requirement: Hybrid redaction model
Redaction SHALL apply, in this order: (1) a built-in denylist that removes known secret-bearing content — authorization and cookie headers, credential-shaped fields, and any value equal to a resolved secret or token used in the run; (2) a per-case denylist of additional field names, headers, JSONPath expressions, or UI selectors/regions to mask; (3) a per-case allowlist that permits capturing specific fields or paths that a built-in rule would otherwise drop. A value removed by the built-in denylist SHALL NOT be re-enabled by an allowlist entry.

#### Scenario: Built-in denylist strips an authorization header
- **WHEN** a captured HTTP request includes an `Authorization` header
- **THEN** that header's value is absent from the redacted evidence, with no per-case rule required

#### Scenario: A resolved secret value is masked wherever it appears
- **WHEN** a run resolves a project secret and that same string appears in a captured response body
- **THEN** the occurrence in the response body is masked in the redacted evidence

#### Scenario: Per-case denylist masks an additional field
- **WHEN** a case declares a denylist rule for the JSONPath `$.customer.email`
- **THEN** the value at that path is masked in every captured body it appears in

#### Scenario: Allowlist cannot re-expose a built-in-denied value
- **WHEN** a case declares an allowlist entry for the `Authorization` header
- **THEN** that header is still absent from the redacted evidence

### Requirement: Redaction fails closed
If a redaction rule cannot be evaluated against a piece of captured evidence (an unparseable body, an invalid expression, an ambiguous match), the affected evidence field SHALL be dropped entirely rather than uploaded in a possibly-unredacted form. The run's own pass/fail outcome SHALL NOT be affected.

#### Scenario: Unparseable body is dropped, not uploaded
- **WHEN** a denylist JSONPath rule is declared but a captured response body is not valid JSON
- **THEN** that body is omitted from the redacted evidence and the case outcome is unchanged

### Requirement: Screenshot redaction is best-effort and labelled
Where an adapter emits screenshot evidence, the CLI SHALL apply declared region or selector masks before upload, and the evidence document SHALL mark screenshot evidence as best-effort-redacted so a viewer does not treat it as a guaranteed-clean artifact.

#### Scenario: Declared region is masked and the screenshot is labelled
- **WHEN** a case declares a screenshot mask region and the UI adapter emits a screenshot
- **THEN** the uploaded image has that region obscured and the evidence entry is marked best-effort-redacted

### Requirement: A value typed into the UI is redacted from the action log by default
When evidence capture is enabled, the CLI-side redaction SHALL mask the value a fill-style UI step typed into the page (the step's `value` parameter, or equivalent) in the recorded action log, so a literal credential or personal detail entered by a case is never uploaded verbatim. A value typed into an input the page reports as a password field SHALL be masked regardless of the parameter name. This masking is part of the built-in denylist (redaction model clause 1): a per-case denylist entry is not required to trigger it.

#### Scenario: A fill step's typed value is masked without a per-case rule
- **WHEN** a case's pipeline fills a login form field with a literal value and the run captures evidence
- **THEN** that literal value is absent from the uploaded evidence's action log for that step

#### Scenario: A password field's value is masked
- **WHEN** a fill step targets an input the page reports as `type="password"`
- **THEN** the typed value is masked in the evidence even if the case did not name it as sensitive

#### Scenario: A non-sensitive typed value can be opted back in
- **WHEN** a case adds an allowlist entry for a specific fill step's value that is not a password field and not a resolved secret
- **THEN** that value appears unmasked in the evidence action log, consistent with how the allowlist re-includes other built-in-name-masked fields

### Requirement: Screenshot evidence is only uploaded for steps that requested it, and only when redaction of its declared masks succeeds
Where an adapter emits a screenshot, the CLI SHALL upload it only after applying every declared region or selector mask for that step. If a declared mask cannot be applied to the image, the screenshot SHALL be dropped rather than uploaded partially masked, consistent with the fail-closed rule for other evidence.

#### Scenario: A screenshot whose declared mask cannot be applied is dropped
- **WHEN** a case declares a screenshot mask that the CLI cannot apply to the captured image
- **THEN** that screenshot is omitted from the uploaded evidence, the step's other evidence is still uploaded, and the case outcome is unchanged

