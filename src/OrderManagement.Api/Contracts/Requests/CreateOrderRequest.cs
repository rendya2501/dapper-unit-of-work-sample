using FluentValidation;

namespace OrderManagement.Api.Contracts.Requests;

/// <summary>
/// 注文作成リクエスト
/// </summary>
/// <remarks>
/// <para><strong>Validation ルール</strong></para>
/// <list type="bullet">
/// <item>CustomerId: 1以上の整数</item>
/// <item>Items: 1件以上必須</item>
/// <item>各アイテム: ProductId は1以上、Quantity は1以上</item>
/// </list>
/// </remarks>
/// <param name="CustomerId">顧客ID</param>
/// <param name="Items">注文アイテムのリスト</param>
public record CreateOrderRequest(int CustomerId, List<OrderItemRequest> Items)
{
    /// <summary>
    /// 注文作成リクエストのバリデーター
    /// </summary>
    public class Validator : AbstractValidator<CreateOrderRequest>
    {
        public Validator()
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
                .SetValidator(new OrderItemRequest.Validator());
        }
    }
};
