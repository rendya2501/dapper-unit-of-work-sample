using System;
using System.Collections.Generic;
using System.Text;

namespace OrderManagement.Application.Services;

public interface IOrderService
{
    Task<int> CreateOrderAsync(int productId, int quantity);
}
