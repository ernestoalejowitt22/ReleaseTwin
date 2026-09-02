## MODIFIED Requirements

### Requirement: Webhook notifications are authenticated and processed idempotently

The endpoint SHALL reject any notification whose signature does not verify against the Merchant of Record's signing secret. It SHALL additionally reject any notification whose signed timestamp is outside a bounded freshness tolerance of the current time, so that a captured, validly-signed notification cannot be replayed later. Each notification SHALL be processed at most once in effect: receiving the same notification again SHALL be acknowledged successfully and produce no additional change. A notification whose processing fails SHALL NOT be recorded as processed and SHALL be retried when the Merchant of Record redelivers it.

#### Scenario: Unsigned or wrongly signed notification is rejected

- **WHEN** a request to the endpoint has a missing or invalid signature
- **THEN** it is rejected and no state changes

#### Scenario: A stale but validly-signed notification is rejected

- **WHEN** a notification carries a correct signature but its signed timestamp is older (or further in the future) than the freshness tolerance
- **THEN** it is rejected, it is not recorded as processed, and no state changes

#### Scenario: Duplicate delivery is a no-op

- **WHEN** a notification that has already been processed is delivered again
- **THEN** the endpoint acknowledges it successfully and the organization's state is unchanged

#### Scenario: Failed processing is retried

- **WHEN** processing a notification fails partway through
- **THEN** the endpoint returns a non-success response, the notification is not marked processed, and a later redelivery is processed normally
