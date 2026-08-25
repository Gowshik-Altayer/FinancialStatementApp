# Architecture

> This document is built up phase by phase alongside the implementation. This revision covers **Phase 1 (solution setup)**, **Phase 2 (Clean Architecture DI wiring)**, **Phase 3 (SQL Server + EF Core)**, **Phase 4 (JWT authentication)**, and **Phase 5 (Angular layout)**.

## Solution layout

```
FinancialStatementAI.sln
├── src/
│   ├── FinancialStatementAI.Domain          (no dependencies)
│   ├── FinancialStatementAI.Application     → Domain
│   ├── FinancialStatementAI.Infrastructure  → Application, Domain
│   ├── FinancialStatementAI.Api             → Application, Infrastructure
│   └── FinancialStatementAI.Worker          → Application, Infrastructure
├── tests/
│   ├── FinancialStatementAI.UnitTests       → Domain, Application, Infrastructure
│   └── FinancialStatementAI.IntegrationTests→ Api
└── frontend/
    └── FinancialStatementAI.Web             (Angular, standalone, included in the .sln via .esproj)
```

## Clean Architecture rule

Dependencies only point inward: `Api`/`Worker` → `Application` → `Domain`, with `Infrastructure`
implementing interfaces declared in `Application`/`Domain` (dependency inversion). Controllers in
`Api` stay thin — they call into `Application` services/handlers; no business logic lives in
controllers or in `Infrastructure`.

## Why .NET 8 target framework on a newer SDK

The dev machine has the .NET 10 SDK installed, but all projects explicitly target `net8.0`
(`<TargetFramework>net8.0</TargetFramework>`) per the challenge's technology stack, and the
`Microsoft.AspNetCore.App` / `Microsoft.NETCore.App` 8.0 shared runtimes are installed side by side.
NuGet packages added to `net8.0` projects must be pinned to 8.x-compatible versions explicitly
(the SDK's own "latest" resolution otherwise offers 10.x packages that don't support net8.0).

## Angular project inside the Visual Studio solution

`FinancialStatementAI.Web` is a **standard Angular CLI application** (created with `ng new`,
buildable with `ng build` / `ng serve` / `ng test` on their own). It is made visible to Visual
Studio 2022 by adding a `FinancialStatementAI.Web.esproj` file next to it, using the
JavaScript Project System (`Microsoft.VisualStudio.JavaScript.Sdk`). The `.esproj` simply maps
`npm run build` / `npm run start` / `npm run clean` to the standard MSBuild Build/Run/Clean
actions — Visual Studio does not own or rewrite the Angular project, it just drives the same npm
scripts a developer would run from the command line. This requires the **Node.js development
tools** individual component (or the "ASP.NET and web development" workload) in Visual Studio
2022 to load; see the root `README.md` for setup steps.

A `proxy.conf.json` in the Angular project forwards `/api` and `/health` calls from
`ng serve` (port 4200) to the ASP.NET Core API's HTTPS dev port (7031 by default, see
`src/FinancialStatementAI.Api/Properties/launchSettings.json`), avoiding CORS friction during
day-to-day development. The API also has a CORS policy (`AngularDevClient`) allowing
`http://localhost:4200` / `https://localhost:4200` directly, for cases where the proxy isn't used
(e.g. running the Angular dev server and API separately without `ng serve`'s proxy).

## Phase 1 acceptance

- Solution opens in Visual Studio 2022 with all 8 projects visible (7 .csproj + 1 .esproj).
- `dotnet build FinancialStatementAI.sln` builds every project, including running `npm install`
  and `ng build` for the Angular project via the `.esproj`.
- `dotnet test FinancialStatementAI.sln` passes: one unit test proving Domain/Application/
  Infrastructure assemblies load and reference each other correctly, one integration test that
  boots the API in-memory (`WebApplicationFactory`) and asserts `GET /health` returns 200.
- `npm test` (Vitest, via Angular CLI) passes for the Angular shell.
- Swagger UI is available at `/swagger` in Development.

## Composition root (Phase 2)

Each layer below `Api`/`Worker` exposes a single `AddXxx(IServiceCollection ...)` extension method
rather than letting the hosts reach into its internals:

- `FinancialStatementAI.Application.DependencyInjection.AddApplication(this IServiceCollection)`
- `FinancialStatementAI.Infrastructure.DependencyInjection.AddInfrastructure(this IServiceCollection, IConfiguration)`

Both `Program.cs` (Api) and `Program.cs` (Worker) call both extension methods at startup. Right now
they're intentionally no-ops (`return services;`) — there's nothing to register yet since Domain/
Application/Infrastructure have no entities or services. Later phases add registrations *inside*
these two methods (EF Core's `AppDbContext` and repositories in Phase 3, FluentValidation
validators in Phase 4+, Hangfire/Redis/OCR/AI/storage services in their respective phases) without
ever touching `Program.cs` again — that's the point of the composition-root pattern: hosts stay
thin, and each layer owns registering its own pieces.

A cross-project unit test (`DependencyInjectionTests`) builds a real `ServiceCollection`, calls
both extensions, and calls `BuildServiceProvider(validateScopes: true)` to catch any future
lifetime/scope mismatches (e.g. a singleton depending on a scoped service) as soon as they're
introduced.

## Persistence (Phase 3)

The 12 Domain entities from requirement #10 (`User`, `Statement`, `Transaction`, `Category`,
`TransactionExtraction`, `TransactionClassification`, `TransactionCorrection`,
`ReconciliationResult`, `ProcessingJob`, `ProcessingError`, `AIRequest`, `AIUsageMetric`) and their
EF Core mapping live in `Domain/Entities` and `Infrastructure/Persistence/Configurations`
respectively — see [docs/database.md](database.md) for the full schema, the reasoning behind
which tables are append-only audit logs vs. mutable current-state rows, and delete-behavior
choices. `AddInfrastructure()` (the Phase 2 composition root) now registers `AppDbContext` against
the `DefaultConnection` connection string.

## Authentication (Phase 4)

`POST /api/auth/register` and `POST /api/auth/login` (`Api/Controllers/AuthController.cs`) are
thin — validation via `IValidator<T>` (FluentValidation), then delegate to `IAuthService`
(`Application/Services/AuthService.cs`), which depends only on three Application-defined
interfaces: `IUserRepository`, `IPasswordHasher`, `IJwtTokenGenerator`. Infrastructure implements
all three (`Infrastructure/Repositories/UserRepository.cs`, `Infrastructure/Security/`) — the
Application layer never references EF Core or any JWT library directly, keeping the dependency
rule intact.

- **Passwords**: PBKDF2-HMAC-SHA256, 210,000 iterations, random 16-byte salt per hash
  (`PasswordHasher`). No `Microsoft.AspNetCore.Identity` dependency pulled in for something this
  self-contained.
- **Tokens**: HMAC-SHA256-signed JWTs (`JwtTokenGenerator`), standard claims (`sub`, `email`,
  `jti`) plus an explicit `ClaimTypes.Role` claim (the claim `[Authorize(Roles = "...")]` checks
  by default). Settings (`Issuer`, `Audience`, `ExpiryMinutes`) live in `appsettings.json`;
  `SigningKey` is deliberately absent there — see Connection strings & secrets in the root README.
  A **development-only placeholder key** lives in `appsettings.Development.json`, clearly labeled;
  production must override it via User Secrets or an environment variable.
- **Roles**: `Admin` / `User` / `Reviewer` (`Domain.Enums.UserRole`). New registrations always get
  `User` — role elevation is an admin action, not something the register endpoint exposes.
- **Swagger** has a Bearer auth scheme wired in (Authorize button in the UI) so protected
  endpoints can be exercised directly from `/swagger` during development/demo.

Integration tests (`AuthControllerTests`) exercise the full register → login → authenticated
`GET /api/auth/me` flow, plus duplicate-email, wrong-password, and validation-failure cases. These
(and `HealthEndpointTests`) now run against a `CustomWebApplicationFactory` that swaps the real
SQL Server `AppDbContext` registration for a fresh EF Core InMemory database per test-class
instance — tests are fully self-contained and don't need a real SQL Server running.

## Angular layout (Phase 5)

`app.routes.ts` splits into three groups:

- `login` / `register` — public, lazy-loaded, outside the authenticated shell.
- `''` (root) — `Shell` (`core/layout/shell/`, a Material toolbar + sidenav), guarded by
  `authGuard`, hosting the authenticated child routes (`dashboard`, `statements`,
  `transactions`, `review`, `reconciliation`, `categories`). Only `dashboard` has a real
  component today; the rest render `PlaceholderPage` (a route-data-driven stand-in — title/note
  come from `data: {...}` via `withComponentInputBinding()`) so every nav link and lazy-loaded
  route already works end-to-end, without pretending a screen is finished before its own phase
  builds it.
- `**` — `NotFound`.

**Core** (`core/`) holds singleton, app-wide pieces — not tied to any one feature:
- `services/auth.service.ts` — holds the current user as a `signal`, persists the `AuthResponse`
  to `localStorage`, and discards it automatically if the token's `expiresAtUtc` has already
  passed by the time the service is constructed (e.g. the tab was closed for a day).
- `guards/auth.guard.ts` (redirects to `/login` if not authenticated) and `guards/role.guard.ts`
  (factory taking allowed roles, redirects to `/dashboard` otherwise) — both `CanActivateFn`s,
  the modern functional guard style rather than class-based guards.
- `interceptors/jwt.interceptor.ts` — attaches `Authorization: Bearer <token>` to every request
  when a token exists.
- `interceptors/error.interceptor.ts` — on `401` logs out and redirects to `/login` (an
  expired/invalid token shouldn't leave the user stuck on a broken screen); on `0` (network
  unreachable) or `5xx` shows a snack bar with a plain-language message. This is the
  requirement-#32 "Angular should show user-friendly error messages" baseline; screens that need
  more specific messaging (e.g. Login's inline "Invalid email or password") layer their own
  handling on top per-request.

**Why `provideAnimationsAsync()` despite `@angular/animations` being flagged deprecated by
Angular**: only the imperative `trigger()/state()/animate()` authoring API is deprecated (in
favor of the new `animate.enter`/`animate.leave` directives for *app-authored* animations).
Angular Material's own components (menu, sidenav, snack bar) still depend on the animations
engine internally in this version — omitting the provider entirely breaks the build (Material's
own bundle imports `@angular/animations/browser`). No custom animation is authored anywhere in
this app; the dependency exists solely because Material needs it.

Verified end-to-end against the live proxy chain (Angular dev server → `proxy.conf.json` →
API): routing, the Reactive Forms on Login/Register, the JWT interceptor, and the error
interceptor's snack bar all confirmed working by actually submitting the Register form in a
browser. The submission itself got a `500` back (no SQL Server in this sandbox — same limitation
noted in `docs/database.md`), which was useful in its own right: it confirmed the error
interceptor's user-friendly-message path end-to-end without needing a database at all.

Further architecture detail (document-processing pipeline, AI classification, reconciliation)
will be appended here as each phase lands.
