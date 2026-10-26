using System.Text.RegularExpressions;
using FinSightIQ.UserService.Core.DTOs;
using FluentValidation;

namespace FinSightIQ.UserService.Core.Validators;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    private static readonly Regex HasUppercase = new(@"[A-Z]", RegexOptions.Compiled);
    private static readonly Regex HasLowercase = new(@"[a-z]", RegexOptions.Compiled);
    private static readonly Regex HasDigit = new(@"[0-9]", RegexOptions.Compiled);

    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(12).WithMessage("Must be at least 12 characters.")
            .Must(p => p != null && HasUppercase.IsMatch(p)).WithMessage("Must contain at least one uppercase letter.")
            .Must(p => p != null && HasLowercase.IsMatch(p)).WithMessage("Must contain at least one lowercase letter.")
            .Must(p => p != null && HasDigit.IsMatch(p)).WithMessage("Must contain at least one digit.");
    }
}
