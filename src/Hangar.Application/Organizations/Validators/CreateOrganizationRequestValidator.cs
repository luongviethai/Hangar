using FluentValidation;
using Hangar.Application.Organizations.Dtos;

namespace Hangar.Application.Organizations.Validators;

public class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MaximumLength(100)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug chỉ gồm chữ thường, số và gạch ngang (ví dụ 'acme-corp').");
    }
}
