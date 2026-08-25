using FinancialStatementAI.Application;
using FinancialStatementAI.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHangfireProcessingServer(builder.Configuration);

var host = builder.Build();
host.Run();
