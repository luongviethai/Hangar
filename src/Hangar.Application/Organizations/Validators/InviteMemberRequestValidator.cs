using FluentValidation;
using Hangar.Application.Organizations.Dtos;

namespace Hangar.Application.Organizations.Validators;

public class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid email address.");

        RuleFor(x => x.Role)
            .IsInEnum();
    }
}
