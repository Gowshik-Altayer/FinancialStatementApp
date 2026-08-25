# Architecture

> This document is built up phase by phase alongside the implementation. This revision covers **Phase 1 (solution setup)**, **Phase 2 (Clean Architecture DI wiring)**, and **Phase 3 (SQL Server + EF Core)**.

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

Further architecture detail (document-processing pipeline, AI classification, reconciliation)
will be appended here as each phase lands.
