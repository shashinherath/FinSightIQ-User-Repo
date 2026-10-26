using System.Text.RegularExpressions;
using FinSightIQ.UserService.Core.DTOs;
using FluentValidation;

namespace FinSightIQ.UserService.Core.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private static readonly Regex HasUppercase = new(@"[A-Z]", RegexOptions.Compiled);
    private static readonly Regex HasLowercase = new(@"[a-z]", RegexOptions.Compiled);
    private static readonly Regex HasDigit = new(@"[0-9]", RegexOptions.Compiled);

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(12).WithMessage("Must be at least 12 characters.")
            .Must(p => p != null && HasUppercase.IsMatch(p)).WithMessage("Must contain at least one uppercase letter.")
            .Must(p => p != null && HasLowercase.IsMatch(p)).WithMessage("Must contain at least one lowercase letter.")
            .Must(p => p != null && HasDigit.IsMatch(p)).WithMessage("Must contain at least one digit.");
    }
}
