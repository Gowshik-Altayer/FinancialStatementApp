# Build context must be the repository root (the sibling project references need it), e.g.:
#   docker build -f src/FinancialStatementAI.Api/Dockerfile -t financialstatementai-api .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy just the .csproj files first so `dotnet restore` is cached across builds whenever only
# source files (not dependencies) change.
COPY ["src/FinancialStatementAI.Api/FinancialStatementAI.Api.csproj", "src/FinancialStatementAI.Api/"]
COPY ["src/FinancialStatementAI.Application/FinancialStatementAI.Application.csproj", "src/FinancialStatementAI.Application/"]
COPY ["src/FinancialStatementAI.Infrastructure/FinancialStatementAI.Infrastructure.csproj", "src/FinancialStatementAI.Infrastructure/"]
COPY ["src/FinancialStatementAI.Domain/FinancialStatementAI.Domain.csproj", "src/FinancialStatementAI.Domain/"]
RUN dotnet restore "src/FinancialStatementAI.Api/FinancialStatementAI.Api.csproj"

COPY src/FinancialStatementAI.Api/ src/FinancialStatementAI.Api/
COPY src/FinancialStatementAI.Application/ src/FinancialStatementAI.Application/
COPY src/FinancialStatementAI.Infrastructure/ src/FinancialStatementAI.Infrastructure/
COPY src/FinancialStatementAI.Domain/ src/FinancialStatementAI.Domain/
RUN dotnet publish "src/FinancialStatementAI.Api/FinancialStatementAI.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# Migrations/seeding are applied by the app itself in Development (see Program.cs) — a real
# deployment applies migrations explicitly (e.g. `dotnet ef database update`) rather than relying
# on that Development-only convenience block, since it never runs in a non-Development container.
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FinancialStatementAI.Api.dll"]
