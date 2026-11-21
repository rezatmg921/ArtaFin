# ADR-0004: Database-backed role and permission authorization

- Status: Accepted
- Date: 2026-11-21

## Context

ArtaFin already has system roles represented by `AppRole`, user assignments represented by `UserRole`, and per-financial-book membership represented by `FinancialBookMember` with the `FinancialBookRole` enum. These are distinct authorization scopes and must not be conflated.

System roles currently appear in JWT role claims. Role claims are useful for identity display and coarse compatibility checks, but they are not sufficient as the source of truth for mutable permissions: changing a role's permissions must take effect without waiting for an access token to expire.

## Decision

### System-wide RBAC

Keep the existing `AppRole` and `UserRole` entities and tables. Do not introduce parallel role or user-role tables.

Add:
- `AppPermission`: a stable, code-defined permission catalog entry (for example `Accounts.Read`, `Accounts.Create`, `Transactions.Approve`). Permission codes are unique and immutable identifiers; display name and description are metadata.
- `RolePermission`: the many-to-many assignment between an existing `AppRole` and an `AppPermission`.

Administrators may create, rename, and delete custom roles and assign any permissions from the supported catalog. They may not create arbitrary permission codes through the API. The catalog is versioned in source code and synchronized idempotently by the initial-data seeder. This keeps authorization policy identifiers reviewable while allowing role composition to change at runtime.

Built-in roles such as `Administrator` and `User` are seeded and protected from rename/deletion. The Administrator role receives the complete supported permission set. The User role receives only the baseline permissions required by normal application use.

### Authorization decision and token freshness

Permission authorization is database-backed, not based on permission claims embedded in JWTs. A permission policy requires an authenticated principal and a permission code; its authorization handler resolves the current user's active role assignments and role-permission links from the database. Consequently, changing a role's permissions or a user's role assignment takes effect on the next authorization evaluation and does not require waiting for token expiry.

JWT role claims remain available for display and backward compatibility, but must not be used as the final authority for permission-protected operations. Access tokens remain short-lived as configured. If a cache is introduced later, it must have explicit invalidation/versioning and bounded staleness; the initial implementation should favor correctness over premature caching.

### Resource-level access is separate

System permissions answer whether a user may perform an operation in principle. They do not grant access to every financial book.

For every operation scoped to a `FinancialBook`, the application must also validate the user's relationship to that exact book:
- owner access is resolved from `FinancialBook.OwnerId`;
- non-owner access is resolved from an active `FinancialBookMember` record;
- `FinancialBookRole` remains a resource-scoped role and is not replaced by system permissions.

The effective authorization decision is the conjunction of the required system permission and access to the target financial book, followed by any operation-specific domain invariant.

### Administrative safeguards

- Only an administrator with the relevant management permissions may manage roles, role-permission assignments, and user-role assignments.
- Built-in roles and the permission catalog cannot be deleted through management endpoints.
- The system must never allow removal, deactivation, or demotion of the last active user who holds the protected Administrator role.
- A caller cannot remove their own administrative access if doing so would leave the system without another active administrator.
- Administrative changes are persisted transactionally and validated against the current database state.

## Initial permission catalog

The initial catalog should be organized by capability and introduced incrementally as endpoints are protected:

- `Users.Read`, `Users.Manage`
- `Roles.Read`, `Roles.Manage`
- `FinancialBooks.Read`, `FinancialBooks.Create`, `FinancialBooks.ManageMembers`
- `Accounts.Read`, `Accounts.Create`, `Accounts.Update`, `Accounts.Delete`
- `Transactions.Read`, `Transactions.Create`, `Transactions.Reverse`, `Transactions.Approve`
- `Leases.Read`, `Leases.Manage`, `Installments.Pay`
- `Reports.Read`

The catalog must be checked against actual endpoint capabilities before seeding; unused or unsupported permissions should not imply that an endpoint is already protected.

## Migration and rollout

1. Add permission domain entities and EF Core configurations while preserving the existing role and membership model.
2. Add a migration that creates the permission catalog and role-permission join table, with unique indexes and restrictive foreign keys.
3. Extend the idempotent seeder to upsert catalog entries and synchronize built-in role grants without overwriting custom-role assignments.
4. Add a permission policy provider and authorization handler that query current grants.
5. Add role and user-role management use cases and endpoints with the safeguards above.
6. Protect API endpoints incrementally with explicit permission policies and resource-level FinancialBook checks.
7. Add positive and negative tests, including anonymous access, authenticated users without grants, allowed grants, permission changes taking effect without token refresh, and cross-book access denial.

## Consequences

- Existing user accounts, role IDs, role assignments, and financial-book memberships are preserved.
- No permissions are copied into JWTs, avoiding stale authorization after role changes.
- Database-backed authorization adds a database lookup to permission-protected requests; a carefully invalidated cache can be considered only after correctness and performance are measured.
- Deployment must apply the new EF migration before enabling endpoints that require the new permission tables.
