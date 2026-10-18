using AssetManagement.Application.Dtos;
using FluentValidation;

namespace AssetManagement.Application.Validators
{
    public class MaintenanceUpsertValidator : AbstractValidator<MaintenanceUpsertDto>
    {
        public MaintenanceUpsertValidator()
        {
            RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
            RuleFor(x => x.Type).IsInEnum();
            RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Date is required.");
            RuleFor(x => x.Vendor).MaximumLength(200);
            RuleFor(x => x.Comments).MaximumLength(2000);
        }
    }
}
