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

## Statements (Phase 6)

All endpoints require `Authorization: Bearer <token>`. A statement is only visible to the user
who uploaded it (`404 Not Found` for another user's statement — see `docs/architecture.md` for
why 404 rather than 403).

### `POST /api/statements/upload`

`multipart/form-data` with a `file` field. Accepts PDF, JPG, JPEG, PNG up to 20 MB. Validates the
file's actual bytes (magic numbers), not just its extension or Content-Type header; for PDFs,
also confirms the file opens and isn't password-protected. Returns immediately after creating the
`Statement` row (status `Uploaded`) and a pending `ProcessingJob` — no OCR/AI processing happens
synchronously (that begins once Hangfire is wired up in Phase 14 and consumes pending jobs).

- `201 Created` → `StatementDetailResponse` (Location header points at `GET /api/statements/{id}`)
- `400 Bad Request` → no file, empty file, unsupported type, oversized, corrupted/password-protected PDF, or content/extension mismatch
- `401 Unauthorized` → missing/invalid token

### `GET /api/statements`

Returns all statements belonging to the current user, most recently uploaded first. No pagination
yet — added in Phase 13 once search/filter lands.

### `GET /api/statements/{id}`

Full statement detail (`StatementDetailResponse`). `404 Not Found` if it doesn't exist or belongs
to someone else.

### `GET /api/statements/{id}/status`

Lightweight `{ id, processingStatus, uploadedAt, processedAt }` — meant for polling processing
progress once background processing exists (Phase 14) without pulling the full detail payload.
