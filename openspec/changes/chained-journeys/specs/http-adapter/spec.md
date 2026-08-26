## ADDED Requirements

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
