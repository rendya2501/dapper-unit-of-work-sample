using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OrderManagement.Api.Filters;

/// <summary>
/// FluentValidation を自動実行する Action Filter
/// </summary>
/// <remarks>
/// <para><strong>なぜこの方法が推奨されるか</strong></para>
/// <list type="bullet">
/// <item>サードパーティライブラリに依存しない</item>
/// <item>シンプルで理解しやすい</item>
/// <item>完全にコントロール可能</item>
/// <item>.NET のバージョンアップに影響されない</item>
/// </list>
/// </remarks>
public class ValidationFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // すべての引数に対してバリデーションを実行
        foreach (var argument in context.ActionArguments)
        {
            if (argument.Value == null) continue;

            var argumentType = argument.Value.GetType();

            // IValidator<T> を取得
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);

            if (serviceProvider.GetService(validatorType) is IValidator validator)
            {
                // バリデーション実行
                var validationContext = new ValidationContext<object>(argument.Value);
                var validationResult = await validator.ValidateAsync(validationContext);

                if (!validationResult.IsValid)
                    throw new ValidationException(validationResult.Errors);
            }
        }

        // バリデーション成功 → 次の処理へ
        await next();
    }
}
