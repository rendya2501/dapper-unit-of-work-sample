using FluentValidation;

namespace OrderManagement.Api.Contracts.Requests;

/// <summary>
/// 在庫作成リクエスト
/// </summary>
/// <param name="ProductName">商品名</param>
/// <param name="Stock">在庫数</param>
/// <param name="UnitPrice">単価</param>
public record CreateInventoryRequest(string ProductName, int Stock, decimal UnitPrice)
{
    /// <summary>
    /// 在庫作成リクエストのバリデーター
    /// </summary>
    public class Validator : AbstractValidator<CreateInventoryRequest>
    {
        public Validator()
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
};
