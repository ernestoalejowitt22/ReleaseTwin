# Security Policy

## Reporting a vulnerability

**Do not open a public issue for security problems.**

Report privately via GitHub's **[Report a vulnerability](https://github.com/ernestoalejowitt22/ReleaseTwin/security/advisories/new)**
(Security → Advisories), or email **ernestoalejo22@gmail.com** with `SECURITY`
in the subject.

Please include: what you found, how to reproduce it, and the impact you
believe it has. A proof of concept helps.

This is a solo-maintained project. Expect an acknowledgement within a few days
and a candid timeline rather than a formal SLA. Coordinated disclosure is
appreciated — we will agree on a date before any public write-up.

## Scope

In scope:

- `ReleaseTwin.Core` and the adapters (`src/`) — especially anything that could
  cause a case to report a false verdict, or leak fixture/secret content.
- CLI-side evidence redaction (`src/`) — a bypass that lets un-redacted
  request/response data or resolved secrets reach an upload is high severity.
- The hosted API and web app (`hosted/`, `web/`) — authn/authz, tenant
  isolation, the ingest path, project secrets storage, and the BFF boundary
  (the browser must never be able to call the API directly).

Out of scope:

- The provisional marketing copy and brand names.
- Findings that require a compromised developer machine or a malicious
  maintainer.
- Denial of service from unrealistic request volumes against a self-hosted
  instance you control.

## Handling of secrets and evidence

ReleaseTwin executes tests in **your** infrastructure. By default only report
metadata (hashes, pass/fail, classification) is uploaded — never fixture
content or secrets. Evidence upload is opt-in, Paid-tier, and redacted in your
own CLI before it leaves your network (auth headers, credential-shaped fields,
and resolved secret values are stripped, plus your allow/deny rules). If you
find a way to defeat that, treat it as a vulnerability and report it privately.

## Supported versions

Pre-1.0: only the latest `main` is supported. Security fixes land on `main`
and in the next tagged release.
