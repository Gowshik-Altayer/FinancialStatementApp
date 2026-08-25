# Architecture

> This document is built up phase by phase alongside the implementation. This revision covers **Phase 1 — solution setup only**.

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

Further architecture detail (DI wiring, entities, document-processing pipeline, AI classification,
reconciliation) will be appended here as each phase lands.
