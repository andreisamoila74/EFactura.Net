# Security policy

## Reporting a vulnerability

**Please do not open a public issue.**

Use GitHub's private reporting:
[**Report a vulnerability**](https://github.com/andreisamoila74/EFactura.Net/security/advisories/new)
(Security → Advisories → Report a vulnerability).

Helpful to include: what the issue is, how to reproduce it, which versions are affected, and what an
attacker could achieve.

Expect an acknowledgement within a few days. This is a personal open-source project maintained in
spare time, so please allow reasonable time for a fix before disclosing publicly. Credit in the
advisory if you would like it.

## Supported versions

Pre-1.0: only the latest release receives fixes.

| Version | Supported |
|---|:--:|
| 0.1.x | ✅ |

## Scope

In scope — this repository's code:

- anything that could leak an access token, or attach one to the wrong request;
- XML injection or entity-expansion through the document model;
- a crash or unbounded resource use while parsing an ANAF response;
- dependency vulnerabilities reachable from shipped code.

Out of scope:

- vulnerabilities in **ANAF's own services** — report those to ANAF;
- how *your* application stores tokens. The SDK deliberately never persists one; storage is the
  host's responsibility, and getting it wrong is an application issue rather than a library one.

## Notes for integrators

A few things worth knowing, none of which are vulnerabilities in this library but all of which have
caused real incidents:

- **An ANAF access token authorises filing tax documents on a company's behalf.** Treat it like a
  credential: encrypt at rest, never log it, never put it in a URL.
- **Response-body logging is off by default** (`LogResponseBodies`). ANAF responses carry counterparty
  identifiers and full document contents. Turn it on to debug, then turn it off.
- **Exception messages and `RawResponse` contain ANAF's response verbatim**, which may include
  business data. Think before forwarding them to an error tracker.
- **The default environment is Test.** Switching to `Production` means documents are real filings.
- **Guard concurrent token refreshes.** Several callers refreshing the same expired token can cause
  ANAF to invalidate the earlier refresh tokens — see
  [docs/oauth.md](docs/oauth.md#the-concurrency-trap).

## Dependencies

`EFactura.Net.Ubl` has no runtime dependencies. `EFactura.Net.Client` depends only on
`Microsoft.Extensions.*`. Dependabot watches both, and CI runs secret scanning on every push.
