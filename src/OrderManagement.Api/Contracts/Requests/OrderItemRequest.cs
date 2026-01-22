using FluentValidation;

namespace OrderManagement.Api.Contracts.Requests;

/// <summary>
/// 注文アイテムリクエスト
/// </summary>
/// <param name="ProductId">商品ID</param>
/// <param name="Quantity">数量</param>
public record OrderItemRequest(int ProductId, int Quantity)
{
    /// <summary>
    /// 注文アイテムリクエストのバリデーター
    /// </summary>
    public class Validator : AbstractValidator<OrderItemRequest>
    {
        public Validator()
        {
            RuleFor(x => x.ProductId)
                .GreaterThan(0);
            //.WithMessage("Product ID must be greater than 0.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0);
            //.WithMessage("Quantity must be greater than 0.");
        }
    }
};
