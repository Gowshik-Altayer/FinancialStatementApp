using System.Text;
using FinancialStatementAI.Api;
using FinancialStatementAI.Application;
using FinancialStatementAI.Infrastructure;
using FinancialStatementAI.Infrastructure.Persistence;
using FinancialStatementAI.Infrastructure.Persistence.Seed;
using FinancialStatementAI.Infrastructure.Security;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

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
        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                    []
                }
            });
        });
        builder.Services.AddHealthChecks();

        // Global exception handling (see GlobalExceptionHandler.cs): catches anything unhandled
        // anywhere in the pipeline, logs it to the ExceptionLogs table, and always returns a
        // generic ProblemDetails response — never a raw exception message/stack trace.
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);

        var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException($"Missing '{JwtSettings.SectionName}' configuration section.");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        builder.Services.AddAuthorization();

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

        // Registered first so it wraps every other middleware/controller action below —
        // anything unhandled anywhere in the request pipeline gets caught here.
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();

            // Dev convenience only: apply pending migrations and seed default data. Migration
            // and seeding are in separate try/catch blocks deliberately — the EF Core InMemory
            // provider (used by integration tests' CustomWebApplicationFactory) doesn't support
            // MigrateAsync at all, and that failure must not prevent seeding from running.
            // Non-fatal if SQL Server isn't reachable yet either (e.g. first run before it's
            // installed) so the rest of the API still starts; production deployments apply
            // migrations explicitly instead of relying on this block.
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            try
            {
                await dbContext.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping database migration — database not reachable or migrations not supported by this provider.");
            }

            try
            {
                await CategorySeeder.SeedAsync(dbContext);
                await MerchantMappingSeeder.SeedAsync(dbContext);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping database seed — database not reachable.");
            }

            // Dashboard is only mapped in Development — see HangfireDashboardAuthorizationFilter
            // for why this API's JWT bearer auth can't meaningfully gate it, and why that's an
            // acceptable tradeoff only because of this environment restriction. No-ops (route
            // never gets mapped) unless "BackgroundJobs:Provider" = "Hangfire" actually
            // registered Hangfire's services in the container.
            if (string.Equals(builder.Configuration["BackgroundJobs:Provider"], "Hangfire", StringComparison.OrdinalIgnoreCase))
            {
                app.UseHangfireDashboard("/hangfire", new DashboardOptions
                {
                    Authorization = [new HangfireDashboardAuthorizationFilter()]
                });
            }
        }

        app.UseHttpsRedirection();

        app.UseCors("AngularDevClient");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health");

        await app.RunAsync();
    }
}
