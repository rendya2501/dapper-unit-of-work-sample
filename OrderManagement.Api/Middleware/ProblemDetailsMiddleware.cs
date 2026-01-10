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
    /// <summary>
    /// 次のミドルウェアを実行し、パイプライン内で発生したすべての例外を捕捉する。
    /// </summary>
    /// <param name="context">HTTP リクエストコンテキスト</param>
    /// <remarks>
    /// このメソッドはミドルウェアのエントリーポイントであり、
    /// 例外はここで必ず捕捉され <see cref="HandleExceptionAsync"/> に委譲される。
    /// </remarks>
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

    /// <summary>
    /// 捕捉した例外を ProblemDetails 形式の HTTP レスポンスに変換する。
    /// </summary>
    /// <param name="context">HTTP コンテキスト</param>
    /// <param name="exception">発生した例外</param>
    /// <remarks>
    /// 例外の種類に応じて HTTPステータスコード、タイトル、詳細、拡張情報を決定する。
    /// </remarks>
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
    /// 例外の種類に応じて ProblemDetails 用の情報を抽出する。
    /// </summary>
    /// <param name="exception">発生した例外</param>
    /// <returns>
    /// HTTP ステータスコード、タイトル、詳細メッセージ、
    /// バリデーションエラーの一覧
    /// </returns>
    /// <remarks>
    /// このメソッドは HTTP 表現の判断のみを行い、
    /// レスポンス生成自体は行わない。
    /// </remarks>
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

    /// <summary>
    /// HTTP ステータスコードに応じて適切なログレベルで例外を記録する。
    /// </summary>
    /// <param name="exception">記録対象の例外</param>
    /// <param name="statusCode">対応する HTTP ステータスコード</param>

    private void LogException(Exception exception, int statusCode)
    {
        if (statusCode >= 500)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(exception, "Internal server error occurred");
            }
        }
        else if (statusCode >= 400)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    exception,
                    "Client error occurred: {Message}",
                    exception.Message);
            }
        }
    }

    /// <summary>
    /// 実行環境に応じてクライアントへ返却する安全なエラーメッセージを取得する。
    /// </summary>
    /// <param name="exception">発生した例外</param>
    /// <returns>クライアント向けエラーメッセージ</returns>

    private string GetSafeErrorMessage(Exception exception)
    {
        // 本番環境では詳細を隠蔽
        if (environment.IsProduction())
        {
            return "An unexpected error occurred. Please contact support.";
        }

        return exception.Message;
    }

    /// <summary>
    /// HTTP ステータスコードに対応する RFC 参照 URI を取得する。
    /// </summary>
    /// <param name="statusCode">HTTP ステータスコード</param>
    /// <returns>ProblemDetails の type フィールドに設定する URI</returns>

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
