using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Requests;
using OrderManagement.Api.Responses;
using OrderManagement.Application.Models;
using OrderManagement.Application.Services.Abstractions;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Api.Controllers;

/// <summary>
/// 注文関連のAPIエンドポイント
/// </summary>
/// <param name="orderService">注文サービス</param>
[ApiController]
[Route("api/[controller]")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    /// <summary>
    /// 注文を作成します
    /// </summary>
    /// <param name="request">注文作成リクエスト</param>
    /// <returns>作成された注文のID</returns>
    /// <response code="200">注文が正常に作成されました</response>
    /// <response code="400">リクエストが不正です</response>
    /// <response code="500">内部サーバーエラー</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        // バリデーションは ValidationFilter が自動実行
        // エラーは ProblemDetailsMiddleware が自動変換

        var items = request.Items
            .Select(i => new OrderItem(i.ProductId, i.Quantity))
            .ToList();

        var orderId = await orderService.CreateOrderAsync(request.CustomerId, items);

        return CreatedAtAction(
            nameof(GetOrderById),
            new { id = orderId },
            new CreateOrderResponse(orderId));
    }

    /// <summary>
    /// すべての注文を取得します
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Order>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await orderService.GetAllOrdersAsync();
        return Ok(orders);
    }

    /// <summary>
    /// IDを指定して注文を取得します
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(int id)
    {
        // NotFoundException は ProblemDetailsMiddleware が自動変換
        var order = await orderService.GetOrderByIdAsync(id);
        return Ok(order);
    }
}
