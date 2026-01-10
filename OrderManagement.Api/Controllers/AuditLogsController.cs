using Microsoft.AspNetCore.Mvc;
using OrderManagement.Application.Services.Abstractions;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Api.Controllers;

/// <summary>
/// 監査ログAPIエンドポイント
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuditLogsController(IAuditLogService auditLogService) : ControllerBase
{
    /// <summary>
    /// すべての監査ログを取得します
    /// </summary>
    /// <param name="limit">取得件数の上限（デフォルト: 100）</param>
    /// <returns>監査ログのリスト（新しい順）</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AuditLog>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int limit = 100)
    {
        var logs = await auditLogService.GetAllAsync(limit);
        return Ok(logs);
    }
}
