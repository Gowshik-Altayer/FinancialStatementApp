# API Documentation

Swagger/OpenAPI is live at `/swagger` in the Development environment (with a Bearer auth scheme
wired in — use the Authorize button after logging in) for whatever endpoints exist at any given
point. Endpoints are added phase by phase (Statements/Upload in Phase 6, Reconciliation in
Phase 11, Transactions/Review in Phase 12, search/filter/pagination in Phase 13, background
processing in Phase 14); this file is filled in as each area is built.

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
`Statement` row (status `Uploaded`) and a `Pending` `ProcessingJob` (stage `Upload`) — no OCR/AI
processing happens automatically on upload; a client calls `POST .../reprocess` (below) to
actually run the pipeline, synchronously or via Hangfire depending on configuration.

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

Lightweight `{ id, processingStatus, uploadedAt, processedAt }` — for polling processing progress
without pulling the full detail payload; the natural thing to poll after a `202 Accepted` from
reprocess (below) when Hangfire is the active provider.

### `POST /api/statements/{id}/reprocess` (Phases 7–14)

Runs the full pipeline (direct PDF text or OCR fallback, statement-field extraction, transaction
parsing/normalization, AI classification, then deterministic reconciliation — see
`docs/ai-processing.md`), either synchronously (default) or via a Hangfire background job
(`BackgroundJobs:Provider` = `Hangfire`, Phase 14) — the URL and verb never change between the two:

- **Synchronous (default)**: `200 OK` → the updated `StatementDetailResponse`, including
  `hasUsableText`, `extractedPageCount`, `extractionMethod` (`"DirectPdfText"` or `"Ocr"`), an
  updated `transactionCount`, and `reconciliationStatus` (`"Reconciled"`, `"Mismatch"`,
  `"InsufficientInformation"`, or `null`).
- **Hangfire-backed**: `202 Accepted` → a `StatementDetailResponse` snapshot with
  `processingStatus: "Processing"` and the statement's *previous* field values (the job hasn't run
  yet) — poll `GET .../status` for progress.
- `404 Not Found` → doesn't exist or belongs to another user

Re-running it replaces the statement's own previously parsed transactions and reclassifies them
from scratch rather than accumulating duplicates, and appends a new reconciliation result rather
than overwriting the previous one — true either way the pipeline actually gets triggered.

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

Applies a human's correction to one or more fields (requirement #9 — date, description, merchant,
amount, type, and category are all correctable). Body: `{ categoryName?, transactionDate?,
description?, merchant?, amount?, transactionType?, reason? }` — every field optional; only the
ones supplied are applied, each producing its own audit row in the returned transaction's
`corrections` array. The original AI-assigned/extracted value for each corrected field is
preserved, never overwritten. A corrected `amount` also re-triggers reconciliation for the
transaction's statement, since it changes the statement's totals.

- `200 OK` → updated `TransactionResponse`
- `400 Bad Request` → `categoryName`/`transactionType` supplied but unrecognized
- `404 Not Found` → the transaction doesn't exist or belongs to another user's statement

### `POST /api/transactions/{transactionId}/corrections/bulk`

The bulk counterpart, scoped to Category only: applies the same category to every transaction the
user owns sharing this one's exact Merchant text, each getting its own audit row. Body:
`{ categoryName, reason? }`.

- `200 OK` → `{ updatedCount, transaction: TransactionResponse }`
- `400 Bad Request` → `categoryName` missing/unrecognized, or the anchor transaction has no merchant to group by
- `404 Not Found` → the transaction doesn't exist or belongs to another user's statement

## Categories (Phase 12)

### `GET /api/categories`

Active categories, for the review UI's correction picker.

- `200 OK` → `{ id, name }[]`

### `POST /api/categories`, `PUT /api/categories/{id}`, `POST /api/categories/{id}/deactivate`, `POST /api/categories/{id}/reactivate`

Full category management (`Admin` role required) — categories are not a fixed list; new ones can
be created and existing ones edited or soft-deleted at runtime.

## Background processing (Phase 14)

Not a REST resource, but relevant to every endpoint that triggers processing (`reprocess` above):

| Config key | Values | Effect |
|---|---|---|
| `BackgroundJobs:Provider` | `Immediate` (default) / `Hangfire` | Whether `reprocess` runs synchronously or is enqueued for a separate `FinancialStatementAI.Worker` process |
| `Hangfire:Storage` | `SqlServer` (default) / `InMemory` | Where Hangfire persists jobs, when it's the active provider — `InMemory` is for local dev/tests without a SQL Server instance, never production |

When Hangfire is active, `/hangfire` (Development environment only) serves the Hangfire Dashboard —
job history, retry/delete controls, server status. See `docs/architecture.md` for why its
authorization filter allows every request (an honest limitation of pairing a JWT-only API with an
interactive dashboard, not a real security boundary) and why it's Development-gated as a result.

## Caching & distributed locks (Phase 15)

Also not a REST resource. `Caching:Provider` = `InMemory` (default) or `Redis` controls both
`ICacheService` (backs the categories cache above) and `IDistributedLockService` (guards
`reprocess` against a second, overlapping run for the same statement — see
`docs/architecture.md`). When set to `Redis`, `Caching:Redis:ConnectionString` is required, and
must point every process (Api and every `FinancialStatementAI.Worker` instance) at the *same*
Redis — the whole point of Redis over the in-process default is that the cache/lock is shared
across processes, which only holds if they're all actually configured to use it.
