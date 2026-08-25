# FinancialStatementAI

AI-assisted financial statement processing platform — ingests bank/credit-card statements (PDF,
scanned PDF, JPG/PNG), extracts and normalizes transactions, classifies them into expense
categories with confidence scoring, reconciles totals, and supports human review/correction.

Built for the DataCaliper AI Innovation Hiring Challenge (Group 3 — Senior).

**Status: Phase 12 of 18 complete.** See [Development Phases](#development-phases) below. Each
phase is implemented and committed on its own branch off `main`, then merged in — see `git log`
for the full history.

## Technology stack

| Layer | Technology |
|---|---|
| Frontend | Angular (standalone components), TypeScript, Angular Material, RxJS, Reactive Forms, Angular Router, HttpClient |
| Backend | ASP.NET Core Web API, .NET 8, C# |
| Database | Microsoft SQL Server, EF Core (+ Dapper for read-heavy queries) |
| Auth | JWT Bearer + role-based authorization |
| Background jobs | Hangfire |
| Cache | Redis |
| File storage | Local (dev) / Azure Blob Storage (prod), behind `IFileStorageService` |
| Document processing | Direct PDF text extraction, OCR, Azure AI Document Intelligence — all behind abstractions |
| AI | Azure OpenAI / OpenAI — behind `ITransactionClassifier`, hybrid rules + merchant mapping + LLM |
| Validation | FluentValidation |
| Logging | Serilog / `Microsoft.Extensions.Logging` |
| Testing | xUnit, Moq, FluentAssertions (backend); Angular CLI unit tests (frontend) |
| Containerization | Docker, Docker Compose |
| IDE | **Visual Studio 2022** |

## Solution structure

```
FinancialStatementAI.sln
├── src/
│   ├── FinancialStatementAI.Api             ASP.NET Core Web API (controllers, DI composition root)
│   ├── FinancialStatementAI.Application     Use cases, DTOs, interfaces, validators
│   ├── FinancialStatementAI.Domain          Entities, enums, value objects — no dependencies
│   ├── FinancialStatementAI.Infrastructure  EF Core, OCR/AI/storage implementations, Hangfire, Redis
│   └── FinancialStatementAI.Worker          Background worker host
├── tests/
│   ├── FinancialStatementAI.UnitTests
│   └── FinancialStatementAI.IntegrationTests
├── frontend/
│   └── FinancialStatementAI.Web             Angular app (also wired into the .sln via .esproj)
├── docs/                                    architecture.md, api.md, database.md, ai-processing.md
└── sample-data/                             Sample statements for exercising the pipeline
```

Dependency direction follows Clean Architecture: `Api`/`Worker` → `Application` → `Domain`, with
`Infrastructure` implementing interfaces defined in `Application`/`Domain`. See
[docs/architecture.md](docs/architecture.md) for details.

## Prerequisites

1. **Visual Studio 2022** (17.8+) with workloads:
   - ASP.NET and web development
   - Node.js development (individual component — needed to load the Angular `.esproj`)
2. **.NET 8 SDK** (or a newer SDK that can still target `net8.0`, e.g. .NET 10 SDK — verify with `dotnet --list-sdks` / `dotnet --list-runtimes`)
3. **Node.js** (LTS) and npm — Angular CLI is used via `npx`/local devDependency, no global install required
4. **SQL Server** (LocalDB, Developer Edition, or a container) — needed starting Phase 3
5. **SQL Server Management Studio** (optional, for inspecting the database)
6. **Redis** (local install or via Docker) — needed starting Phase 15
7. Git

## Running from Visual Studio 2022

1. Open `FinancialStatementAI.sln`.
2. Let NuGet restore run automatically (or **Build → Restore NuGet Packages**).
3. Right-click `FinancialStatementAI.Web` → the JavaScript Project System runs `npm install`
   automatically on first build/restore; if it doesn't, open a terminal in
   `frontend/FinancialStatementAI.Web` and run `npm install` manually.
4. **Build → Build Solution** (builds every backend project and runs `ng build` for the Angular
   project via its `.esproj`).
5. To run both API and frontend together, set **multiple startup projects**:
   Solution → right-click → **Configure Startup Projects…** → Multiple startup projects → set
   `FinancialStatementAI.Api` and `FinancialStatementAI.Web` both to **Start**. Press F5.
   - Alternatively, run just `FinancialStatementAI.Api` (F5) and start the Angular dev server from
     a terminal (`npm start` inside `frontend/FinancialStatementAI.Web`) — equivalent for
     day-to-day frontend work since it hot-reloads faster outside of VS's own runner.
6. The Angular dev server (`http://localhost:4200`) proxies `/api` and `/health` calls to the API's
   HTTPS port (see `proxy.conf.json` and `src/FinancialStatementAI.Api/Properties/launchSettings.json`
   — defaults to `https://localhost:7031`), so no CORS configuration is needed for local development
   (a CORS policy for `localhost:4200` exists regardless, for the non-proxied case).

## Running from the command line

```bash
# Backend: build + test the whole solution
dotnet build FinancialStatementAI.sln
dotnet test FinancialStatementAI.sln

# Backend: run just the API (Swagger at https://localhost:7031/swagger, health at /health)
dotnet run --project src/FinancialStatementAI.Api

# Frontend
cd frontend/FinancialStatementAI.Web
npm install
npm start        # ng serve, proxies /api to the backend
npm run build    # production build
npm test         # Angular unit tests (Vitest via Angular CLI)
```

## Database migrations (from Phase 3 onward)

Command line:
```bash
dotnet ef migrations add InitialCreate --project src/FinancialStatementAI.Infrastructure --startup-project src/FinancialStatementAI.Api
dotnet ef database update --project src/FinancialStatementAI.Infrastructure --startup-project src/FinancialStatementAI.Api
```

Visual Studio Package Manager Console (set **Default project** to `FinancialStatementAI.Infrastructure`):
```powershell
Add-Migration InitialCreate -StartupProject FinancialStatementAI.Api
Update-Database -StartupProject FinancialStatementAI.Api
```

## Connection strings & secrets

Configured via `appsettings.Development.json` for non-sensitive defaults and **.NET User Secrets**
for anything sensitive (JWT signing key, Azure/OpenAI keys, storage connection strings) — never
committed to source control.

`appsettings.Development.json` currently ships a **placeholder** JWT signing key (clearly labeled)
so the app runs out of the box for local development. Replace it with your own via User Secrets
before doing anything beyond local dev:

```bash
cd src/FinancialStatementAI.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:SigningKey" "a-long-random-string-at-least-32-characters"
```

(Or in Visual Studio: right-click `FinancialStatementAI.Api` → **Manage User Secrets**.)

To use Azure Blob Storage instead of local disk for uploaded statements, set
`FileStorage:Provider` to `Azure` in `appsettings.json` and add the connection string via the
same User Secrets mechanism: `dotnet user-secrets set "FileStorage:Azure:ConnectionString" "..."`.

OCR and Document Intelligence default to Mock implementations (no configuration needed). To use
real Azure services instead, set `Ocr:Provider` / `DocumentIntelligence:Provider` to `Azure` and
add the corresponding endpoint/key via User Secrets:
```bash
dotnet user-secrets set "Azure:Vision:Endpoint" "https://<resource>.cognitiveservices.azure.com/"
dotnet user-secrets set "Azure:Vision:ApiKey" "..."
dotnet user-secrets set "Azure:DocumentIntelligence:Endpoint" "https://<resource>.cognitiveservices.azure.com/"
dotnet user-secrets set "Azure:DocumentIntelligence:ApiKey" "..."
```

Transaction classification defaults to a Mock LLM (honest low-confidence "Other" for anything
Rules/Merchant Mapping/prior corrections can't place — see `docs/ai-processing.md`). To use a
real LLM, set `Classification:Provider` to `OpenAI` or `AzureOpenAI`:
```bash
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
# or
dotnet user-secrets set "Azure:OpenAI:Endpoint" "https://<resource>.openai.azure.com/"
dotnet user-secrets set "Azure:OpenAI:ApiKey" "..."
dotnet user-secrets set "Azure:OpenAI:DeploymentName" "gpt-4o-mini"
```

Example SQL Server connection strings:

```
# Windows/Integrated auth
Server=localhost;Database=FinancialStatementAI;Trusted_Connection=True;TrustServerCertificate=True;

# SQL auth
Server=localhost;Database=FinancialStatementAI;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;
```

## Development phases

This project is built and committed phase by phase, each on its own branch, per the plan below.
Completed phases are checked off as they land.

- [x] Phase 1 — Visual Studio solution setup
- [x] Phase 2 — Clean Architecture wiring (DI composition, extensions)
- [x] Phase 3 — SQL Server + EF Core (entities, `AppDbContext`, migrations, seed data)
- [x] Phase 4 — Authentication (JWT, roles, login/registration)
- [x] Phase 5 — Angular layout (routing, Material shell, core/shared, auth, dashboard shell)
- [x] Phase 6 — File upload (Angular + API + validation + storage + Statement creation)
- [x] Phase 7 — Digital PDF text extraction + text-quality detection
- [x] Phase 8 — OCR / Document Intelligence / Vision abstraction
- [x] Phase 9 — Transaction extraction + normalization
- [x] Phase 10 — AI classification (rules → merchant mapping → LLM → confidence)
- [x] Phase 11 — Reconciliation (deterministic financial calculations)
- [x] **Phase 12** — Human review UI + audit trail (original vs. corrected values)
- [ ] Phase 13 — Search / filter / pagination
- [ ] Phase 14 — Hangfire background processing
- [ ] Phase 15 — Redis caching / distributed locks
- [ ] Phase 16 — Testing (backend xUnit/Moq/FluentAssertions, Angular tests)
- [ ] Phase 17 — Docker + Docker Compose
- [ ] Phase 18 — Documentation pass

## AI usage disclosure

Built with AI-assisted development (Claude Code) per the challenge's AI usage & disclosure
requirement — used for scaffolding, code generation, and documentation across phases. Prompt
history is exported alongside the submission as required.
