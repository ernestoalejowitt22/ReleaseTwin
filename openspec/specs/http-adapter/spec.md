# http-adapter Specification

## Purpose

A vendor-neutral HTTP adapter that lets a case test any REST API by supplying request details and assertions as case-file data, with no bespoke adapter code required per target API — the mechanism that closes the gap between "the mechanics work" and "a customer can test their own workflow."

## Requirements

### Requirement: Parameterized HTTP request execution
The adapter SHALL provide an operation that issues an HTTP request using a method, URL, optional headers, and optional body supplied entirely as step parameters, and SHALL make the response (status code and body) available for a later step in the same case to assert against.

#### Scenario: Request parameters drive the actual HTTP call
- **WHEN** a step invokes the HTTP request operation with a method, URL, and body supplied as parameters
- **THEN** the adapter issues exactly that HTTP request and the response is available to subsequent steps in the same case

### Requirement: JSONPath assertion against the last response
The adapter SHALL provide an operation that evaluates a JSONPath expression against the most recent HTTP response body and compares the result to an expected value, both supplied as step parameters. A match SHALL be reported as an operation pass; a mismatch SHALL be reported as an operation failure with the actual and expected values in the failure detail.

#### Scenario: Matching JSONPath assertion passes
- **WHEN** a JSONPath expression evaluated against the last response body equals the expected value supplied as a parameter
- **THEN** the operation reports success

#### Scenario: Mismatched JSONPath assertion fails with detail
- **WHEN** a JSONPath expression evaluated against the last response body does not equal the expected value
- **THEN** the operation reports failure, and the failure detail includes both the actual and expected values

### Requirement: No credentials required to install
The adapter SHALL be installable and usable without any credential or environment-specific configuration, since authentication (if the target API needs it) is supplied per-request via step parameters (e.g. an `Authorization` header), not at adapter-construction time.

#### Scenario: Adapter installs with no configuration
- **WHEN** the adapter is installed into a composition with no arguments beyond its own construction
- **THEN** it registers successfully and its operations are available for cases to reference
