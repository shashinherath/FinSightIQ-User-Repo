using System.Text.RegularExpressions;
using FinSightIQ.UserService.Core.DTOs;
using FluentValidation;

namespace FinSightIQ.UserService.Core.Validators;

public sealed class WatchlistAddRequestValidator : AbstractValidator<WatchlistAddRequest>
{
    private static readonly Regex SymbolFormat = new(@"^[A-Z0-9.\-]{1,10}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public WatchlistAddRequestValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty().WithMessage("Symbol is required.")
            .MaximumLength(10).WithMessage("Symbol cannot exceed 10 characters.")
            .Must(s => !string.IsNullOrWhiteSpace(s) && SymbolFormat.IsMatch(s.Trim()))
            .WithMessage("Symbol must consist of 1 to 10 alphanumeric characters, dots, or hyphens.");
    }
}
