# API Documentation

Swagger/OpenAPI is live at `/swagger` in the Development environment (with a Bearer auth scheme
wired in — use the Authorize button after logging in) for whatever endpoints exist at any given
point. Endpoints are added phase by phase (Statements/Upload in Phase 6, Reconciliation in
Phase 11, Transactions/Review in Phase 12, search/filter/pagination in Phase 13); this file is
filled in as each area is built.

Paginated endpoints all return the same shape:

```json
{ "items": [ /* ... */ ], "totalCount": 0, "page": 1, "pageSize": 20 }
```

`page`/`pageSize` are clamped server-side (default page size 20, max 100) rather than trusted
verbatim from the query string.

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

### `GET /api/statements` (Phase 13: search/filter/pagination)

Returns a page of the current user's statements, most recently uploaded first. All query
parameters are optional:

| Parameter | Meaning |
|---|---|
| `search` | Case-insensitive substring match against file name, provider name, or account holder name |
| `status` | Exact match against `processingStatus` (`Uploaded`, `Processing`, `ExtractionFailed`, `ExtractionComplete`, `ClassificationComplete`, `PendingReview`, `Verified`) |
| `reconciliationStatus` | Exact match against the statement's *latest* reconciliation status |
| `page`, `pageSize` | 1-based page number; `pageSize` clamped to 1–100, default 20 |

- `200 OK` → `PagedResult<StatementSummaryResponse>`

### `GET /api/statements/{id}`

Full statement detail (`StatementDetailResponse`). `404 Not Found` if it doesn't exist or belongs
to someone else.

### `GET /api/statements/{id}/status`

Lightweight `{ id, processingStatus, uploadedAt, processedAt }` — meant for polling processing
progress once background processing exists (Phase 14) without pulling the full detail payload.

### `POST /api/statements/{id}/reprocess` (Phases 7–11)

Runs the full pipeline (direct PDF text or OCR fallback, statement-field extraction, transaction
parsing/normalization, AI classification, then deterministic reconciliation — see
`docs/ai-processing.md`) and returns the updated `StatementDetailResponse`, including
`hasUsableText`, `extractedPageCount`, `extractionMethod` (`"DirectPdfText"` or `"Ocr"`), an
updated `transactionCount`, and `reconciliationStatus` (`"Reconciled"`, `"Mismatch"`,
`"InsufficientInformation"`, or `null` if reconciliation hasn't run yet). Runs synchronously today;
from Phase 14 onward this enqueues a Hangfire job and returns `202 Accepted` instead, without
changing the URL or verb. Re-running it replaces the statement's own previously parsed transactions
and reclassifies them from scratch rather than accumulating duplicates, and appends a new
reconciliation result rather than overwriting the previous one.

- `200 OK` → updated `StatementDetailResponse`
- `404 Not Found` → doesn't exist or belongs to another user

### `GET /api/statements/{id}/reconciliation` (Phase 11)

The most recent reconciliation run for this statement — `{ status, expectedClosingBalance,
discrepancy, notes, reconciledAt }` (see `docs/ai-processing.md` for what each `status` value
means and why there are three of them, not two).

- `200 OK` → `ReconciliationResponse`
- `404 Not Found` → the statement doesn't exist or belongs to another user, **or** it exists but
  has never been reconciled yet (no successful reprocess run to completion) — the client can tell
  the difference from the statement's own `processingStatus`

### `POST /api/statements/{id}/verify` (Phase 12)

Marks a statement `Verified` — the terminal state after a human reviewer is satisfied with its
classified transactions and reconciliation result. Only valid from `PendingReview`.

- `200 OK` → updated `StatementDetailResponse`
- `400 Bad Request` → the statement isn't currently `PendingReview`
- `404 Not Found` → doesn't exist or belongs to another user

## Transactions & review (Phases 12–13)

All endpoints require `Authorization: Bearer <token>`; ownership is enforced the same way as
Statements (404, not 403, for another user's data).

### `GET /api/statements/{statementId}/transactions`

Every transaction on one statement, in date order, each with its current (possibly
human-corrected) category, classification confidence/method/reason, a computed `reviewPriority`
(`HighConfidence` / `ReviewRecommended` / `ReviewRequired`, mirroring
`ClassificationConfidenceThresholds`), and its full correction audit trail.

- `200 OK` → `TransactionResponse[]`
- `404 Not Found` → the statement doesn't exist or belongs to another user

### `GET /api/transactions/review-queue`

The cross-statement human review queue: every transaction belonging to one of the current user's
`PendingReview` statements, ordered by classification confidence ascending (the transactions most
likely to need a correction come first).

- `200 OK` → `TransactionResponse[]`

### `GET /api/transactions` (Phase 13: search/filter/pagination)

The "All Transactions" page — every transaction across all of the current user's statements,
regardless of processing status (unlike the review queue, which is PendingReview-only). Optional
`search` (description/merchant substring), `categoryId`, `statementId`, `page`, `pageSize`.

- `200 OK` → `PagedResult<TransactionResponse>`

### `POST /api/transactions/{transactionId}/corrections`

Applies a human's category correction (requirement #9). Body: `{ categoryName, reason? }`. Scoped
to Category only for now — see `docs/ai-processing.md` for why. The original AI-assigned category
is preserved in the returned transaction's `corrections` array, never overwritten.

- `200 OK` → updated `TransactionResponse`
- `400 Bad Request` → `categoryName` missing or doesn't match any active category
- `404 Not Found` → the transaction doesn't exist or belongs to another user's statement

## Categories (Phase 12)

### `GET /api/categories`

Active categories, for the review UI's correction picker. Full category management
(create/edit/deactivate) is a later phase.

- `200 OK` → `{ id, name }[]`
