## Purpose

Bounds how fast any single caller can hit the hosted platform's unauthenticated
and token-authenticated request surface, so a hostile or misconfigured client
cannot flood the Lambda Function URL (which has no CloudFront/WAF in front) or
grind against credential and share-link checks without cost.

## ADDED Requirements

### Requirement: The ingest surface is rate limited per token

The hosted platform SHALL enforce a per-API-token request-rate ceiling on the
ingest endpoints. A token that exceeds the ceiling SHALL receive a `429`
response with a `Retry-After` header, and the rejected request SHALL NOT store
a report, store evidence, or increment usage. The ceiling SHALL be set high
enough that a project running its full case suite on every CI build, including
retries and parallel jobs, never reaches it under normal use.

#### Scenario: A burst beyond the ceiling is throttled

- **WHEN** a single API token issues ingest requests faster than the configured per-token ceiling
- **THEN** requests above the ceiling receive `429` with `Retry-After`, and no report, evidence, or usage increment is recorded for them

#### Scenario: Normal CI traffic is never throttled

- **WHEN** a project uploads one report per case for a suite of the maximum supported size on a single build
- **THEN** every request is accepted and none receives `429`

#### Scenario: One token's throttling does not affect another

- **WHEN** token A is being throttled for exceeding its ceiling
- **THEN** token B for a different project continues to be served normally

### Requirement: The anonymous share-link surface is rate limited per client

The hosted platform SHALL enforce a per-client-address request-rate ceiling on
the unauthenticated share-link resolve and screenshot routes. A client that
exceeds the ceiling SHALL receive `429`. The ceiling SHALL allow a person
opening a shared evidence page, including all of its referenced screenshots, to
load it fully without being throttled.

#### Scenario: Share-token guessing is throttled

- **WHEN** a client requests many distinct share-link tokens in rapid succession from one address
- **THEN** requests above the ceiling receive `429` and reveal nothing about whether any token was valid

#### Scenario: A shared page loads fully

- **WHEN** a viewer opens a shared evidence page that references the maximum number of screenshots
- **THEN** the page and every screenshot load without any request receiving `429`

### Requirement: The billing webhook endpoint is rate limited

The hosted platform SHALL cap the request rate accepted by the billing webhook
endpoint from a single client address. Requests above the cap SHALL be rejected
before signature verification is attempted.

#### Scenario: Webhook flood is shed cheaply

- **WHEN** a client posts to the billing webhook endpoint faster than the configured cap
- **THEN** excess requests are rejected without running signature verification and without any billing state change

### Requirement: Rate limiting fails closed only for the caller, never for the platform

Rate-limit accounting SHALL NOT cause a request that is within its ceiling to be
rejected, and a failure of the rate-limit mechanism itself SHALL be logged and
SHALL default to allowing the request rather than blocking all traffic.

#### Scenario: Rate-limit backend error does not take down ingest

- **WHEN** the rate-limit accounting mechanism is unavailable
- **THEN** in-ceiling requests continue to be served and the degradation is logged
