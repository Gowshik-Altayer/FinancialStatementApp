using FinancialStatementAI.Application;
using FinancialStatementAI.Infrastructure;
using FinancialStatementAI.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
