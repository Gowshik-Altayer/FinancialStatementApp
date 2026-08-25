# Build context must be the repository root (the sibling project references need it), e.g.:
#   docker build -f src/FinancialStatementAI.Worker/Dockerfile -t financialstatementai-worker .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/FinancialStatementAI.Worker/FinancialStatementAI.Worker.csproj", "src/FinancialStatementAI.Worker/"]
COPY ["src/FinancialStatementAI.Application/FinancialStatementAI.Application.csproj", "src/FinancialStatementAI.Application/"]
COPY ["src/FinancialStatementAI.Infrastructure/FinancialStatementAI.Infrastructure.csproj", "src/FinancialStatementAI.Infrastructure/"]
COPY ["src/FinancialStatementAI.Domain/FinancialStatementAI.Domain.csproj", "src/FinancialStatementAI.Domain/"]
RUN dotnet restore "src/FinancialStatementAI.Worker/FinancialStatementAI.Worker.csproj"

COPY src/FinancialStatementAI.Worker/ src/FinancialStatementAI.Worker/
COPY src/FinancialStatementAI.Application/ src/FinancialStatementAI.Application/
COPY src/FinancialStatementAI.Infrastructure/ src/FinancialStatementAI.Infrastructure/
COPY src/FinancialStatementAI.Domain/ src/FinancialStatementAI.Domain/
RUN dotnet publish "src/FinancialStatementAI.Worker/FinancialStatementAI.Worker.csproj" -c Release -o /app/publish --no-restore

# The plain runtime image (not aspnet) — Worker never serves HTTP, it only runs Hangfire's
# BackgroundJobServer (see AddHangfireProcessingServer, Phase 14).
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FinancialStatementAI.Worker.dll"]
