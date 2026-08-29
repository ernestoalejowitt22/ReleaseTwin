## Purpose

An opt-in, advisory layer that classifies a failed visual (screenshot-baseline) assertion as
rendering-noise or a real regression and explains it in prose — strictly on top of, and never
in place of, the deterministic pixel-diff verdict that decides pass/fail.

## ADDED Requirements

### Requirement: Visual analysis is opt-in and off by default
Visual analysis SHALL run for a case only when it is explicitly enabled for that run. When it is
not enabled, execution, the case report, the exit code, and any upload SHALL be identical to the
behavior before this capability existed.

#### Scenario: Analysis disabled leaves the run unchanged
- **WHEN** a case with a failing visual assertion is run without visual analysis enabled
- **THEN** no analysis result is produced and the report, exit code, and upload are byte-for-byte
  what they would be without this capability

#### Scenario: Analysis enabled produces a result for a failed visual assertion
- **WHEN** a case with a failing visual assertion is run with visual analysis enabled
- **THEN** an analysis result is produced and attached to that assertion's step

### Requirement: The pixel-diff verdict is authoritative and analysis never changes it
The deterministic pixel-diff outcome of a visual assertion SHALL remain the sole authority for
that step's pass/fail. A visual-analysis result SHALL NOT change a step outcome, a case report
outcome, a failure classification, a flag-proof adjudication, or the CLI exit code.

#### Scenario: Analysis calling a failure "rendering-noise" does not make the step pass
- **WHEN** a visual assertion fails the pixel diff and analysis classifies it as `rendering-noise`
- **THEN** the step, and the case report, still record the assertion as failed, and the exit code
  is unchanged from the analysis-disabled run

#### Scenario: Analysis result is absent from flag-proof adjudication inputs
- **WHEN** a flag-proof case includes a visual assertion and analysis is enabled
- **THEN** the known-bad / known-good adjudication is computed only from the deterministic step
  outcomes, with no analysis result as an input

### Requirement: Analysis input is the redacted screenshot triplet
Visual analysis SHALL operate only on the post-redaction baseline image, actual image, and diff
image emitted by the failed assertion. It SHALL NOT receive any pre-redaction image, and SHALL
NOT receive video.

#### Scenario: Only redacted images are sent for analysis
- **WHEN** analysis runs for a failed visual assertion
- **THEN** the images provided to the analyzer are the same post-redaction images that
  `evidence-capture` produced for upload, and no other image or recording is included

### Requirement: Analysis produces a fixed, location-neutral result schema
An analysis result SHALL contain: a classification of exactly one of `rendering-noise`,
`real-regression`, or `inconclusive`; a confidence value; a short human-readable explanation; and
a stamp identifying the analyzer model and the prompt version that produced it. The schema SHALL
be identical whether the model ran as a hosted service or as a customer-hosted model.

#### Scenario: Result carries a classification, confidence, explanation, and version stamp
- **WHEN** an analysis result is produced
- **THEN** it has one of the three defined classifications, a confidence, a prose explanation, and
  a model + prompt-version stamp

#### Scenario: A customer-hosted analyzer returns the same schema
- **WHEN** the same triplet is analyzed by a customer-hosted model instead of the hosted service
- **THEN** the result has the same schema and field set, differing only in field values and the
  version stamp

### Requirement: Analysis failure degrades gracefully
If analysis is unavailable, errors, or exceeds its time budget, the run, its report, its exit
code, and its upload SHALL be unaffected, and the result for that assertion SHALL be recorded as
`analysis-unavailable`.

#### Scenario: Analyzer timeout does not affect the run
- **WHEN** the analyzer does not respond within its time budget for a failed visual assertion
- **THEN** the case report and exit code are identical to the analysis-disabled run, and the
  assertion's analysis result is recorded as `analysis-unavailable`

### Requirement: A rendering-noise classification cannot extend retries
A `rendering-noise` classification MAY be used as an input to an automatic retry decision, but
only within the retry count and timeout the case already declares. It SHALL NOT increase the
number of retries, extend a timeout, or cause a step that has exhausted its declared retries to
be retried again.

#### Scenario: Noise classification retries only within the declared policy
- **WHEN** a visual assertion is classified `rendering-noise` and the case declares up to 2
  retries
- **THEN** at most the 2 declared retries occur, and if all fail the assertion is reported failed

#### Scenario: No declared retries means no retry
- **WHEN** a visual assertion is classified `rendering-noise` and the case declares no retries
- **THEN** the assertion is reported failed with no retry attempt

### Requirement: Analysis is per-tenant isolated
A single analyzer request SHALL contain evidence from exactly one organization. Analysis results
SHALL be stored and readable only within the organization of the report they describe, on the
same terms as that evidence.

#### Scenario: One organization's images are never batched with another's
- **WHEN** the hosted analysis service processes triplets from two organizations
- **THEN** no analyzer request contains images from more than one organization

#### Scenario: Analysis results are not visible across organizations
- **WHEN** an analysis result is stored for a report in organization A
- **THEN** no user outside organization A can retrieve or view it under any view or filter

### Requirement: Hosted visual analysis requires a Paid-tier organization
Accepting a triplet for hosted analysis and storing its result SHALL require the uploading
token's organization to be on the Paid tier. A request from a Free-tier organization SHALL be
rejected with a distinct, non-fatal signal, and the run SHALL be otherwise unaffected.

#### Scenario: Free-tier analysis request is rejected without failing the run
- **WHEN** a Free-tier organization's CLI requests hosted visual analysis
- **THEN** no analysis result is stored, the response indicates analysis was not accepted, and the
  case report, exit code, and metadata upload proceed as normal
