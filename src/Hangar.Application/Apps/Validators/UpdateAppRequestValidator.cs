using FluentValidation;
using Hangar.Application.Apps.Dtos;

namespace Hangar.Application.Apps.Validators;

public class UpdateAppRequestValidator : AbstractValidator<UpdateAppRequest>
{
    public UpdateAppRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Platform)
            .IsInEnum();

        RuleFor(x => x.PackageId)
            .NotEmpty().WithMessage("PackageId is required.")
            .MaximumLength(200);
    }
}
