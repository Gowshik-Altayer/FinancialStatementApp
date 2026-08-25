# API Documentation

Swagger/OpenAPI is live at `/swagger` in the Development environment (with a Bearer auth scheme
wired in — use the Authorize button after logging in) for whatever endpoints exist at any given
point. Endpoints are added phase by phase (Statements/Upload in Phase 6, Transactions/Review in
Phase 12, Reconciliation in Phase 11, Dashboard/Search in Phase 13); this file is filled in as
each area is built.

## Authentication (Phase 4)

### `POST /api/auth/register`

Anonymous. Body: `{ email, password, firstName, lastName }`. Password must be 8-128 characters.
Always registers as role `User`.

- `200 OK` → `AuthResponse` (`token`, `expiresAtUtc`, `userId`, `email`, `firstName`, `lastName`, `role`)
- `400 Bad Request` → validation problem details (invalid email, password too short, etc.)
- `409 Conflict` → email already registered

### `POST /api/auth/login`

Anonymous. Body: `{ email, password }`.

- `200 OK` → `AuthResponse` (same shape as register)
- `400 Bad Request` → validation problem details
- `401 Unauthorized` → invalid email/password, or account deactivated

### `GET /api/auth/me`

Requires `Authorization: Bearer <token>`.

- `200 OK` → `{ userId, email, name, role }` read from the token's claims
- `401 Unauthorized` → missing/invalid/expired token
