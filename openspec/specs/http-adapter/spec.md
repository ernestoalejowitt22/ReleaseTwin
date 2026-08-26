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

### Requirement: http.request can declare captures from its own response
An `http.request` step MAY declare one or more captures drawn from its own response (a JSON field
via a path expression, a response header, a cookie); when declared, the adapter SHALL populate them
per the `value-capture` capability.

#### Scenario: A JSON field from the response is captured
- **WHEN** an `http.request` step declares a capture of a JSON field present in its response body
- **THEN** that field's value is available to later steps under the declared capture name

### Requirement: http.request parameters can reference captured values
Any string parameter of an `http.request` step (URL, header value, body content) MAY reference a
value captured by an earlier step; the adapter SHALL resolve that reference at execution time per
`value-capture`.

#### Scenario: A captured token is used as a bearer header
- **WHEN** an earlier step captured a token and a later `http.request` step's header value
  references that capture
- **THEN** the request is sent with the captured token's actual value in that header

### Requirement: An OAuth2 client-credentials exchange is a single convenience step
A case MAY perform a standard OAuth2 client-credentials grant (token endpoint, client ID/secret,
optional scope) as one step, without hand-assembling the request. The adapter SHALL make the
resulting access token capturable the same way any other `http.request` capture is.

#### Scenario: Client-credentials exchange yields a usable token
- **WHEN** a case declares an OAuth2 client-credentials step against a real token endpoint with
  valid credentials
- **THEN** the resulting access token is captured and usable by later steps exactly like any other
  captured value

### Requirement: HTTP Basic auth can be expressed without hand-encoding
A case MAY supply a username and password for an `http.request` step; the adapter SHALL construct
the corresponding Basic auth header automatically, without the case author base64-encoding it
themselves.

#### Scenario: Basic auth header is built from username and password
- **WHEN** an `http.request` step declares a username and password for Basic auth
- **THEN** the request is sent with a correctly-encoded `Authorization: Basic ...` header
