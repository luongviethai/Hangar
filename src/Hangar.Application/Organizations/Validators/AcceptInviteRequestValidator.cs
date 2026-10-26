using FluentValidation;
using Hangar.Application.Organizations.Dtos;

namespace Hangar.Application.Organizations.Validators;

public class AcceptInviteRequestValidator : AbstractValidator<AcceptInviteRequest>
{
    public AcceptInviteRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token is required.");
    }
}
