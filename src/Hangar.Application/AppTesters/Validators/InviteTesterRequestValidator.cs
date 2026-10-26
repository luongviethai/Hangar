using FluentValidation;
using Hangar.Application.AppTesters.Dtos;

namespace Hangar.Application.AppTesters.Validators;

public class InviteTesterRequestValidator : AbstractValidator<InviteTesterRequest>
{
    public InviteTesterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid email address.");
    }
}
