## ADDED Requirements

### Requirement: The evidence view surfaces the advisory visual-analysis result as distinct from the verdict
Where a run's evidence includes a failed screenshot-baseline assertion with a visual-analysis
result, the dashboard SHALL display that result's classification, confidence, prose explanation,
and analyzer model + prompt-version stamp. The display SHALL be visually distinct from, and
clearly subordinate to, the deterministic pass/fail verdict, and SHALL be labelled advisory.

#### Scenario: A classified visual failure shows both the verdict and the advisory result
- **WHEN** a signed-in user views a report whose evidence has a failed visual assertion with a
  `rendering-noise` analysis result
- **THEN** the assertion is shown as failed (the verdict), and the `rendering-noise` classification,
  confidence, explanation, and analyzer version stamp are shown separately and labelled advisory

#### Scenario: An unavailable analysis result is shown as such
- **WHEN** a failed visual assertion has an `analysis-unavailable` result
- **THEN** the view indicates analysis was unavailable and still shows the assertion as failed

#### Scenario: No analysis result shows only the verdict
- **WHEN** a failed visual assertion has no analysis result
- **THEN** the view shows the assertion as failed with no advisory analysis section
