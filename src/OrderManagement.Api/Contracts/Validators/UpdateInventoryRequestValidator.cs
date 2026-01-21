using FluentValidation;
using OrderManagement.Api.Contracts.Requests;

namespace OrderManagement.Api.Contracts.Validators;

/// <summary>
/// 在庫更新リクエストのバリデーター
/// </summary>
public class UpdateInventoryRequestValidator : AbstractValidator<UpdateInventoryRequest>
{
    public UpdateInventoryRequestValidator()
    {
        RuleFor(x => x.ProductName)
            .NotEmpty()
            //.WithMessage("Product name is required.")
            .MaximumLength(100);
        //.WithMessage("Product name must not exceed 100 characters.");

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0);
        //.WithMessage("Stock must be greater than or equal to 0.");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0);
            //.WithMessage("Unit price must be greater than 0.");
    }
}