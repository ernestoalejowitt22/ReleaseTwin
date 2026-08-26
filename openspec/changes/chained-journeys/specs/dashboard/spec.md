## ADDED Requirements

### Requirement: A customer can visually author a journey
The dashboard SHALL provide a visual builder for composing a journey: an ordered pipeline of steps
across whatever adapters are relevant, with captured values from one step wireable into a later
step's parameters. Saving a journey from the builder SHALL create a new version in `hosted-journeys`.

#### Scenario: Saving from the builder creates a new version
- **WHEN** a customer saves changes made in the visual builder
- **THEN** a new, immutable journey version is created reflecting those changes

#### Scenario: Wiring a capture is visible in the builder
- **WHEN** a customer connects one step's captured value to a later step's parameter in the builder
- **THEN** the saved journey version reflects that connection using the same capture-reference
  mechanism a hand-written case file would use
