namespace OrderManagement.Domain.Exceptions;

/// <summary>
/// リソースが見つからない場合にスローされる例外
/// </summary>
/// <remarks>
/// <para><strong>配置場所</strong></para>
/// <para>
/// Domain 層に配置する理由：
/// - ドメインロジック（ビジネスルール）の一部
/// - Infrastructure 層の実装詳細ではない
/// - Application 層やドメイン層からスローされる
/// </para>
/// 
/// <para><strong>使用例</strong></para>
/// <code>
/// var order = await repository.GetByIdAsync(id) 
///     ?? throw new NotFoundException($"Order {id} not found.");
/// </code>
/// </remarks>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} with key '{key}' was not found.")
    {
    }
}