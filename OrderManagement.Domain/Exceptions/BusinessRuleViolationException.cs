namespace OrderManagement.Domain.Exceptions;

/// <summary>
/// ビジネスルール違反の場合にスローされる例外
/// </summary>
/// <remarks>
/// InvalidOperationException の代わりに使用する、
/// より明示的なビジネス例外。
/// </remarks>
public class BusinessRuleViolationException : Exception
{
    public BusinessRuleViolationException(string message) : base(message)
    {
    }

    public BusinessRuleViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
