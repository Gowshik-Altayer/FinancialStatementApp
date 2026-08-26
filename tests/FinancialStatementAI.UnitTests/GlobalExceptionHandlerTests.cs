using System.Security.Claims;
using System.Text.Json;
using FinancialStatementAI.Api;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinancialStatementAI.UnitTests;

public class GlobalExceptionHandlerTests
{
    private readonly Mock<IExceptionLogRepository> _exceptionLogRepository = new();

    private GlobalExceptionHandler CreateHandler()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _exceptionLogRepository.Object);
        var provider = services.BuildServiceProvider();

        return new GlobalExceptionHandler(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<GlobalExceptionHandler>.Instance);
    }

    private static DefaultHttpContext CreateHttpContext(string method = "GET", string path = "/api/whatever", Guid? userId = null)
    {
        var context = new DefaultHttpContext
        {
            Request = { Method = method, Path = path },
            Response = { Body = new MemoryStream() }
        };

        if (userId.HasValue)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())], "TestAuth"));
        }

        return context;
    }

    [Fact]
    public async Task Logs_The_Exception_To_The_Repository_With_Request_Details()
    {
        var httpContext = CreateHttpContext("POST", "/api/statements/upload");
        var exception = new InvalidOperationException("something broke");

        ExceptionLog? captured = null;
        _exceptionLogRepository
            .Setup(r => r.AddAsync(It.IsAny<ExceptionLog>(), It.IsAny<CancellationToken>()))
            .Callback<ExceptionLog, CancellationToken>((log, _) => captured = log)
            .Returns(Task.CompletedTask);

        await CreateHandler().TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(typeof(InvalidOperationException).FullName, captured!.ExceptionType);
        Assert.Equal("something broke", captured.Message);
        Assert.Equal("/api/statements/upload", captured.RequestPath);
        Assert.Equal("POST", captured.RequestMethod);
        Assert.Equal(StatusCodes.Status500InternalServerError, captured.StatusCode);
    }

    [Fact]
    public async Task Extracts_The_Current_UserId_From_The_Request_When_Authenticated()
    {
        var userId = Guid.NewGuid();
        var httpContext = CreateHttpContext(userId: userId);

        ExceptionLog? captured = null;
        _exceptionLogRepository
            .Setup(r => r.AddAsync(It.IsAny<ExceptionLog>(), It.IsAny<CancellationToken>()))
            .Callback<ExceptionLog, CancellationToken>((log, _) => captured = log)
            .Returns(Task.CompletedTask);

        await CreateHandler().TryHandleAsync(httpContext, new Exception("boom"), CancellationToken.None);

        Assert.Equal(userId, captured!.UserId);
    }

    [Fact]
    public async Task Leaves_UserId_Null_For_An_Unauthenticated_Request()
    {
        var httpContext = CreateHttpContext();

        ExceptionLog? captured = null;
        _exceptionLogRepository
            .Setup(r => r.AddAsync(It.IsAny<ExceptionLog>(), It.IsAny<CancellationToken>()))
            .Callback<ExceptionLog, CancellationToken>((log, _) => captured = log)
            .Returns(Task.CompletedTask);

        await CreateHandler().TryHandleAsync(httpContext, new Exception("boom"), CancellationToken.None);

        Assert.Null(captured!.UserId);
    }

    [Fact]
    public async Task Returns_A_Generic_500_ProblemDetails_Response_Never_The_Raw_Exception_Message()
    {
        var httpContext = CreateHttpContext();
        _exceptionLogRepository.Setup(r => r.AddAsync(It.IsAny<ExceptionLog>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handled = await CreateHandler().TryHandleAsync(httpContext, new Exception("a secret internal detail"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        using var json = JsonDocument.Parse(body);

        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("a secret internal detail", body);
    }

    [Fact]
    public async Task A_Failure_To_Persist_The_Log_Entry_Still_Returns_A_Response_Rather_Than_Throwing()
    {
        var httpContext = CreateHttpContext();
        _exceptionLogRepository
            .Setup(r => r.AddAsync(It.IsAny<ExceptionLog>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var handled = await CreateHandler().TryHandleAsync(httpContext, new Exception("original failure"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
    }
}
