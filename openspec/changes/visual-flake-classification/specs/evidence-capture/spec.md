## MODIFIED Requirements

### Requirement: Screenshot redaction is best-effort and labelled
Where an adapter emits screenshot evidence, the CLI SHALL apply declared region or selector masks
before upload, and the evidence document SHALL mark screenshot evidence as best-effort-redacted
so a viewer does not treat it as a guaranteed-clean artifact. When a screenshot-baseline
assertion emits a baseline/actual/diff triplet, each of the three images SHALL be redacted by the
same declared region or selector masks before upload, and each SHALL be marked
best-effort-redacted. Only the post-redaction triplet SHALL be usable as input to visual
analysis; no code path SHALL submit a pre-redaction image for analysis or upload.

#### Scenario: Declared region is masked and the screenshot is labelled
- **WHEN** a case declares a screenshot mask region and the UI adapter emits a screenshot
- **THEN** the uploaded image has that region obscured and the evidence entry is marked
  best-effort-redacted

#### Scenario: All three triplet images are redacted before upload or analysis
- **WHEN** a screenshot-baseline assertion fails, a mask region is declared, and evidence capture
  is enabled
- **THEN** the baseline, actual, and diff images each have that region obscured, each is marked
  best-effort-redacted, and any visual analysis receives only these post-redaction images

## ADDED Requirements

### Requirement: Screen recordings are opt-in, not redaction-guaranteed, and not proof
Screen-recording evidence SHALL be captured only when recording is explicitly enabled for the
run, independently of the screenshot evidence toggle. A recording SHALL be marked in the evidence
document as not-redaction-guaranteed and explicitly not-proof. No recording SHALL contribute to
any evidence hash, any case or step outcome, or any flag-proof adjudication. A recording SHALL
NOT be submitted to visual analysis as part of normal run execution.

#### Scenario: Recording is labelled not-proof and excluded from hashing
- **WHEN** a run uploads evidence that includes a screen recording
- **THEN** the recording entry is marked not-redaction-guaranteed and not-proof, and no evidence
  hash in the document is computed over the recording

#### Scenario: Recording is never auto-analyzed during a run
- **WHEN** a case runs with both recording and visual analysis enabled
- **THEN** visual analysis runs only on failed-assertion screenshot triplets, and the recording
  is not sent to any analyzer as part of the run
