using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using OrderManagement.Domain.Exceptions;

namespace OrderManagement.Api.Middleware;

/// <summary>
/// すべての例外を ProblemDetails 形式で返すミドルウェア
/// </summary>
/// <remarks>
/// <para><strong>RFC 7807 準拠</strong></para>
/// <para>
/// ProblemDetails は RFC 7807 で定義された標準フォーマット。
/// すべてのエラーレスポンスを統一することで、
/// クライアント側のエラーハンドリングが容易になる。
/// </para>
/// 
/// <para><strong>設計原則</strong></para>
/// <list type="bullet">
/// <item>すべての例外を1箇所でキャッチ</item>
/// <item>ProblemDetails 形式で統一</item>
/// <item>例外の種類に応じて適切な HTTP ステータスコードを返す</item>
/// <item>本番環境では詳細なエラー情報を隠蔽</item>
/// </list>
/// </remarks>
public class ProblemDetailsMiddleware(
    RequestDelegate next,
    ILogger<ProblemDetailsMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // C# 13: switch 式で明示的にタプルを使用
        var (statusCode, title, detail, errors) = GetErrorDetails(exception);

        // ログ出力
        LogException(exception, statusCode);

        // ProblemDetails レスポンス作成
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = GetProblemType(statusCode)
        };

        // バリデーションエラーの場合は詳細を追加
        if (errors.Any())
        {
            problemDetails.Extensions["errors"] = errors;
        }

        // 開発環境では詳細情報を追加
        if (environment.IsDevelopment() && exception is not ValidationException)
        {
            problemDetails.Extensions["exceptionType"] = exception.GetType().Name;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    /// <summary>
    /// 例外の種類に応じてエラー詳細を返す
    /// </summary>
    private (int StatusCode, string Title, string Detail, IEnumerable<ValidationError> Errors)
        GetErrorDetails(Exception exception)
    {
        return exception switch
        {
            // FluentValidation の検証エラー
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                "One or more validation errors occurred.",
                validationEx.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage))
            ),

            // リソースが見つからない
            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "Not Found",
                notFoundEx.Message,
                Enumerable.Empty<ValidationError>()
            ),

            // ビジネスルール違反
            BusinessRuleViolationException businessEx => (
                StatusCodes.Status400BadRequest,
                "Business Rule Violation",
                businessEx.Message,
                Enumerable.Empty<ValidationError>()
            ),

            // 上記以外の例外（予期しないエラー）
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                GetSafeErrorMessage(exception),
                Enumerable.Empty<ValidationError>()
            )
        };
    }

    private void LogException(Exception exception, int statusCode)
    {
        if (statusCode >= 500)
        {
            logger.LogError(exception, "Internal server error occurred");
        }
        else if (statusCode >= 400)
        {
            logger.LogWarning(exception, "Client error occurred: {Message}", exception.Message);
        }
    }

    private string GetSafeErrorMessage(Exception exception)
    {
        // 本番環境では詳細を隠蔽
        if (environment.IsProduction())
        {
            return "An unexpected error occurred. Please contact support.";
        }

        return exception.Message;
    }

    private static string GetProblemType(int statusCode)
    {
        return statusCode switch
        {
            400 => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            404 => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            500 => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            _ => "https://tools.ietf.org/html/rfc7231"
        };
    }
}

/// <summary>
/// バリデーションエラーの詳細
/// </summary>
public record ValidationError(string Property, string Error);


//public sealed class ProblemDetailsMiddleware(RequestDelegate next)
//{
//    public async Task InvokeAsync(HttpContext context)
//    {
//        try
//        {
//            await next(context);
//        }
//        catch (ValidationException ex)
//        {
//            await WriteProblem(context, StatusCodes.Status400BadRequest,
//                "Validation Error", ex.Message);
//        }
//        catch (NotFoundException ex)
//        {
//            await WriteProblem(context, StatusCodes.Status404NotFound,
//                "Not Found", ex.Message);
//        }
//        catch (Exception ex)
//        {
//            await WriteProblem(context, StatusCodes.Status500InternalServerError,
//                "Internal Server Error", ex.Message);
//        }
//    }

//    private static async Task WriteProblem(
//        HttpContext context,
//        int status,
//        string title,
//        string detail)
//    {
//        context.Response.StatusCode = status;
//        context.Response.ContentType = "application/problem+json";

//        var problem = new ProblemDetails
//        {
//            Status = status,
//            Title = title,
//            Detail = detail,
//            Instance = context.Request.Path
//        };

//        await context.Response.WriteAsJsonAsync(problem);
//    }
//}
