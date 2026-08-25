using System.Security.Claims;
using FinancialStatementAI.Application.DTOs.Auth;
using FinancialStatementAI.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancialStatementAI.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IAuthService authService,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var validation = await registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var result = await authService.RegisterAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return Conflict(new ProblemDetails { Title = "Registration failed", Detail = result.Error, Status = StatusCodes.Status409Conflict });
        }

        return Ok(result.Response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var result = await authService.LoginAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return Unauthorized(new ProblemDetails { Title = "Login failed", Detail = result.Error, Status = StatusCodes.Status401Unauthorized });
        }

        return Ok(result.Response);
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me()
    {
        return Ok(new
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Email = User.FindFirstValue(ClaimTypes.Email),
            Name = User.FindFirstValue(ClaimTypes.Name),
            Role = User.FindFirstValue(ClaimTypes.Role)
        });
    }
}
