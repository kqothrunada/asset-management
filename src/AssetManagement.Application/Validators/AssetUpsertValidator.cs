using AssetManagement.Application.Dtos;
using AssetManagement.Domain;
using FluentValidation;

namespace AssetManagement.Application.Validators
{
    public class AssetUpsertValidator : AbstractValidator<AssetUpsertDto>
    {
        public AssetUpsertValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Category).IsInEnum();
            RuleFor(x => x.Subcategory).IsInEnum();
            RuleFor(x => x.Status).IsInEnum();

            RuleFor(x => x.Subcategory)
                .Must((dto, sub) => AssetRules.IsValidPair(dto.Category, sub))
                .When(x => Enum.IsDefined(x.Category) && Enum.IsDefined(x.Subcategory))
                .WithMessage("Subcategory does not match Category.");

            RuleFor(x => x.Description)
                .Must(AssetRules.HasText)
                .When(x => x.Status == AssetStatus.New)
                .WithMessage("Description is required.");
        }
    }
}
