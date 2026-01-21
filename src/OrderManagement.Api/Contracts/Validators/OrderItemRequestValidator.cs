using FluentValidation;
using OrderManagement.Api.Contracts.Requests;

namespace OrderManagement.Api.Contracts.Validators;

/// <summary>
/// 注文アイテムリクエストのバリデーター
/// </summary>
public class OrderItemRequestValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0);
        //.WithMessage("Product ID must be greater than 0.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0);
            //.WithMessage("Quantity must be greater than 0.");
    }
}
