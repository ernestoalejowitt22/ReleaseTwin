## ADDED Requirements

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
