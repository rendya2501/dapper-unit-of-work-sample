using FluentValidation;
using OrderManagement.Api.Contracts.Requests;

namespace OrderManagement.Api.Contracts.Validators;

/// <summary>
/// 注文作成リクエストのバリデーター
/// </summary>
public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        // 顧客IDは1以上
        RuleFor(x => x.CustomerId)
            .GreaterThan(0);
        //.WithMessage("Customer ID must be greater than 0.");

        // アイテムは必須
        RuleFor(x => x.Items)
            .NotEmpty();
            //.WithMessage("Order must have at least one item.");

        // 各アイテムのバリデーション
        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemRequestValidator());
    }
}
