using FinancialStatementAI.Application;
using FinancialStatementAI.Infrastructure;
using FinancialStatementAI.Infrastructure.Persistence;
using FinancialStatementAI.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace FinancialStatementAI.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddHealthChecks();

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AngularDevClient", policy =>
            {
                policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();

            // Dev convenience only: apply pending migrations and seed default categories.
            // Non-fatal if SQL Server isn't reachable yet (e.g. first run before it's
            // installed, or under a test host with no database configured) so the rest of
            // the API still starts; production deployments apply migrations explicitly.
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            try
            {
                await dbContext.Database.MigrateAsync();
                await CategorySeeder.SeedAsync(dbContext);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping database migration/seed — database not reachable.");
            }
        }

        app.UseHttpsRedirection();

        app.UseCors("AngularDevClient");

        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health");

        await app.RunAsync();
    }
}
