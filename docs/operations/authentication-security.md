# Authentication security operations

This document describes the initial operational controls for ArtaFin's self-hosted authentication API.

## Request rate limiting

The API applies the named `auth` fixed-window policy to:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`

The initial policy allows 10 requests per minute per observed remote IP address. Excess requests receive HTTP `429 Too Many Requests`. Requests are not queued.

The policy is an application-process in-memory limiter. Its counters are not shared across multiple API instances and reset when the process restarts. If ArtaFin is scaled to multiple API instances, use a distributed rate-limit store or enforce an additional limit at the trusted reverse proxy / gateway.

### Reverse proxy and client IP

The limiter keys requests using `HttpContext.Connection.RemoteIpAddress`. When hosted directly, this is the connecting client address. When hosted behind IIS, ARR, a load balancer, or another reverse proxy, the API may see the proxy address instead.

Do not trust arbitrary `X-Forwarded-For` headers. Before enabling forwarded-header processing, explicitly configure the known proxy addresses or networks for the deployment and place forwarded-header middleware before rate limiting. Verify the observed client address from the deployed topology. Otherwise, users behind the same proxy may share one rate-limit bucket.

## Password and token controls

- Password hashes use the versioned PBKDF2-HMAC-SHA256 format implemented by `Pbkdf2PasswordHasher`.
- Refresh tokens are generated from cryptographically secure random bytes; only their SHA-256 hashes are persisted.
- Access-token signing keys and database connection strings must be supplied through protected deployment configuration, never committed to source control.
- Production database migrations are an explicit deployment step; the API must not apply migrations automatically at startup.

## Verification checklist

Before production exposure:

1. Send 10 requests to an authentication endpoint from one client IP within a minute and verify the requests are admitted subject to normal endpoint validation.
2. Send another request in the same window and verify HTTP 429.
3. Verify a different client IP has an independent bucket.
4. Repeat the test through the actual reverse-proxy topology and confirm the API sees the intended client IP.
5. Verify the limiter resets after the one-minute window.
6. Test login lockout and refresh-token rotation/reuse independently; rate limiting is an additional layer, not a replacement for those controls.
7. Add integration tests for the 429 behavior before production release.
