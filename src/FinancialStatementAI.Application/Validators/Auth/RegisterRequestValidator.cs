using FinancialStatementAI.Application.DTOs.Auth;
using FluentValidation;

namespace FinancialStatementAI.Application.Validators.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(128);
    }
}
