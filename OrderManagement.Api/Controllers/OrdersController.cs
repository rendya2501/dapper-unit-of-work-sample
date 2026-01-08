using Microsoft.AspNetCore.Mvc;
using OrderManagement.Application.Services;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(IOrderService orderService) : ControllerBase
{

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            var orderId = await orderService.CreateOrderAsync(request.ProductId, request.Quantity);
            return Ok(new { OrderId = orderId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Internal server error", Details = ex.Message });
        }
    }
}

public record CreateOrderRequest(int ProductId, int Quantity);
