# ADR 0003: Self-hosted custom authentication

- Status: Accepted
- Date: 2026-11-07

## Context

ArtaFin is a self-hosted modular monolith deployed on a Windows Server VM with SQL Server 2022. The product should own its local account, credential, session, token, role, and permission lifecycle and must not require an external identity provider.

The current API uses ASP.NET Core Identity for user persistence and password verification, together with JWT bearer authentication. That implementation is an initial foundation, not a production commitment.

## Decision

ArtaFin will not use ASP.NET Core Identity.

We will implement an application-owned authentication and access-management module. It will remain self-hosted and persist its data in the ArtaFin SQL Server database.

The module will own:
- user registration, account status, and normalized login identifiers;
- password credential lifecycle, password changes, and recovery workflows;
- login throttling and temporary account lockout;
- access-token issuance and validation;
- refresh-token rotation, reuse detection, and revocation;
- device/session inventory and logout;
- roles and explicit permissions.

We will use established cryptographic primitives and standards rather than inventing cryptography or token formats. Password hashing must use a reviewed, configurable password-hashing implementation with per-password salts and an upgrade path. Access tokens will use standard JWT validation. Refresh tokens will be high-entropy random secrets; only their hashes will be persisted.

Authentication (proving who the caller is) and authorization (deciding what the caller may do) remain separate concerns. Financial-book ownership and resource-level authorization must continue to be enforced in the Application layer and data-access paths; a role claim alone is not sufficient.

## Data and migration constraints

The existing initial migration and model snapshot currently describe the finance schema. No Identity schema migration has been committed. The custom authentication schema must be introduced through a reviewed EF Core migration.

Do not rewrite or delete an already-applied migration. If a database has already been provisioned, use additive migrations and an explicit data transition plan. Never run schema changes automatically at API startup.

## Security requirements

- Never store plaintext passwords, access tokens, or refresh tokens.
- Use constant-time verification where applicable.
- Normalize and uniquely constrain login identifiers in the database.
- Return generic authentication failures to avoid account enumeration.
- Apply rate limiting and lockout controls to credential endpoints.
- Validate JWT signature, issuer, audience, expiry, and token identifier.
- Keep signing keys and connection strings outside source control.
- Rotate refresh tokens on use and detect reuse of revoked tokens.
- Revoke sessions on password reset, credential compromise, and account disablement.
- Audit security-sensitive events without logging secrets or sensitive financial data.
- Protect cookies with Secure, HttpOnly, and appropriate SameSite settings if a browser-cookie client is introduced; otherwise define a secure bearer-token storage strategy per client.

## Consequences

Benefits:
- No runtime dependency on an external identity provider.
- Full ownership of ArtaFin's account and session lifecycle.
- Data model and workflows can evolve with product requirements.

Costs and risks:
- ArtaFin assumes responsibility for security review, testing, operational monitoring, and maintenance of the authentication module.
- The module must receive dedicated unit, integration, abuse-case, and migration tests before production use.
- Features commonly supplied by Identity (password recovery, lockout, token providers, role management) must be deliberately designed and tested.

## Implementation sequence

1. Define the user, credential, session, refresh-token, role, and permission data model.
2. Add persistence mappings, indexes, constraints, and migration.
3. Implement password hashing and verification behind an application abstraction.
4. Implement registration, login, refresh, logout, and session revocation use cases.
5. Integrate JWT bearer validation and authorization policies without Identity.
6. Add rate limiting, audit events, tests, and operational documentation.
7. Remove Identity packages, registrations, types, and obsolete documentation.
