using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DNASoftech.Infrastructure.Data;
using DNASoftech.Domain.Models.ECommerce;
using DNASoftech.Application.Interface;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace DNASoftech.Web.Controllers
{
    /// <summary>
    /// API controller for order creation and customer order history.
    /// Route: /api/orders/*
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly DNASoftechDB _db;
        private readonly IEmailService _emailService;
        private readonly ILogger<OrdersController> _logger;
        private readonly IPaymentService _paymentService;

        public OrdersController(DNASoftechDB db, IEmailService emailService, ILogger<OrdersController> logger, IPaymentService paymentService)
        {
            _db = db;
            _emailService = emailService;
            _logger = logger;
            _paymentService = paymentService;
        }

        private static string UpsertStatusTag(string? details, string status)
        {
            var source = details ?? string.Empty;
            var parts = source.Split('|', StringSplitOptions.RemoveEmptyEntries)
                              .Where(p => !p.TrimStart().StartsWith("status:", StringComparison.OrdinalIgnoreCase))
                              .ToList();
            parts.Add($"status:{status}");
            return string.Join('|', parts);
        }

        /// <summary>Returns orders for the currently authenticated user.</summary>
        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMyOrders()
        {
            if (User?.Identity?.IsAuthenticated != true) return Unauthorized();
            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var orders = await _db.Orders
                .Where(o => o.CustomerEmail.ToLower() == email.ToLower())
                .OrderByDescending(o => o.CreatedAtUtc)
                .Select(o => new
                {
                    id = o.OrderId,
                    o.CustomerName,
                    o.CustomerEmail,
                    total = o.TotalAmount,
                    status = o.Status.ToString(),
                    paymentMethod = o.PaymentMethod.ToString(),
                    createdAt = o.CreatedAtUtc,
                    o.ItemsJson,
                    customerAddress = o.CustomerAddress
                })
                .ToListAsync();

            return Ok(orders);
        }

        /// <summary>Creates a new order from the checkout page payload.</summary>
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (dto == null) return BadRequest("Missing payload");
            if (dto.Customer == null) return BadRequest("Missing customer details");
            if (dto.Items == null || dto.Items.Length == 0) return BadRequest("No items in order");

            try
            {
                // Map payment method string to enum
                var payMethodStr = dto.Payment?.Method?.ToLowerInvariant() ?? "unknown";
                var payMethod = payMethodStr switch
                {
                    "cod" => DNASoftech.Domain.Enums.PaymentMethod.Cash,
                    "cash" => DNASoftech.Domain.Enums.PaymentMethod.Cash,
                    "upi" => DNASoftech.Domain.Enums.PaymentMethod.Upi,
                    "card" => DNASoftech.Domain.Enums.PaymentMethod.Card,
                    "netbanking" => DNASoftech.Domain.Enums.PaymentMethod.NetBanking,
                    _ => DNASoftech.Domain.Enums.PaymentMethod.Unknown
                };

                var order = new Order
                {
                    UserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "guest",
                    ItemsJson = System.Text.Json.JsonSerializer.Serialize(dto.Items),
                    CustomerName = dto.Customer.Name ?? string.Empty,
                    CustomerEmail = dto.Customer.Email ?? string.Empty,
                    CustomerPhone = dto.Customer.Phone ?? string.Empty,
                    CustomerAddress = dto.Customer.Address ?? string.Empty,
                    TotalAmount = dto.Total,
                    PaymentMethod = payMethod,
                    PaymentDetails = dto.Payment?.Details,
                    Status = DNASoftech.Domain.Enums.OrderStatus.PendingConfirmation
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                // Notify admins
                try
                {
                    var adminEmails = await _db.AppUsers
                        .Where(u => u.Role != null && u.Role.ToLower() == "admin" && u.Email != null && u.Email != "")
                        .Select(u => u.Email)
                        .ToListAsync();

                    var itemsHtml = string.Join("", dto.Items.Select(i =>
                        $"<li>ProductId: {i.ProductId} × Qty: {i.Quantity}</li>"));

                    var body = $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><title>New Order Alert</title></head>
<body style=""margin:0;padding:0;background:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;"">
  <table width=""100%"" style=""max-width:600px;margin:40px auto;background:#fff;border-radius:12px;box-shadow:0 4px 15px rgba(0,0,0,.05);overflow:hidden;"">
    <tr><td style=""padding:24px;background:#0f172a;text-align:center;"">
      <h1 style=""color:#fff;margin:0;font-size:22px;"">New Order Received</h1>
    </td></tr>
    <tr><td style=""padding:32px;"">
      <div style=""background:#fffbeb;border-left:4px solid #f59e0b;padding:16px;margin-bottom:24px;border-radius:0 8px 8px 0;"">
        <h3 style=""margin:0;color:#b45309;"">Action Required</h3>
        <p style=""margin:0;color:#d97706;"">This order is pending your review.</p>
      </div>
      <p><strong>Order ID:</strong> #{order.OrderId}</p>
      <p><strong>Total:</strong> &#8377;{order.TotalAmount:N2}</p>
      <p><strong>Customer:</strong> {order.CustomerName} ({order.CustomerEmail})</p>
      <p><strong>Phone:</strong> {order.CustomerPhone}</p>
      <p><strong>Address:</strong> {order.CustomerAddress}</p>
      <p><strong>Payment:</strong> {order.PaymentMethod}</p>
      <h4>Items:</h4>
      <ul>{itemsHtml}</ul>
      <a href=""/Admin"" style=""display:inline-block;background:#2874f0;color:#fff;text-decoration:none;font-size:16px;font-weight:600;padding:14px 32px;border-radius:8px;"">Open Admin Dashboard</a>
    </td></tr>
    <tr><td style=""background:#f8fafc;padding:20px;text-align:center;border-top:1px solid #e2e8f0;"">
      <p style=""color:#94a3b8;font-size:12px;margin:0;"">DNA Softech Shop Internal Automated Notification</p>
    </td></tr>
  </table>
</body></html>";

                    await _emailService.SendEmailAsync(adminEmails!, $"New Order #{order.OrderId} - Pending Confirmation", body);
                }
                catch (Exception mailEx)
                {
                    _logger.LogWarning(mailEx, "Failed to send admin notification for order {Id}", order.OrderId);
                }

                // Send confirmation email to customer acknowledging order placement
                try
                {
                    var customerBody = $@"<!doctype html>
<html>
<body style='font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Arial;margin:0;padding:24px;background:#f8fafc;'>
  <div style='max-width:560px;margin:0 auto;background:#fff;padding:24px;border-radius:12px;box-shadow:0 8px 30px rgba(2,6,23,0.06)'>
    <h2 style='margin-top:0;color:#0f172a'>Thanks for your order, {System.Net.WebUtility.HtmlEncode(order.CustomerName)}!</h2>
    <p style='color:#475569'>We received your order <strong>#{order.OrderId}</strong>. Our team will review it and you will receive another email when your order is approved and being dispatched.</p>
    <h4 style='color:#0f172a'>Order Summary</h4>
    <p style='color:#475569'>Total: &#8377;{order.TotalAmount:N2}</p>
    <p style='color:#64748b;font-size:13px'>If you have any questions, reply to this email.</p>
    <p style='margin-top:18px;color:#64748b;font-size:13px'>Cheers,<br/>DNA Softech Team</p>
  </div>
</body>
</html>";

                    await _emailService.SendEmailAsync(new[] { order.CustomerEmail }, $"Your Order #{order.OrderId} - Received", customerBody);
                }
                catch (Exception mailEx)
                {
                    _logger.LogWarning(mailEx, "Failed to send customer order acknowledgement for order {Id}", order.OrderId);
                }

                // Call Payment Gateway if online processing is required
                if (payMethod != DNASoftech.Domain.Enums.PaymentMethod.Cash && dto.Payment?.Method?.ToLower() == "payu")
                {
                    var pr = new DNASoftech.Application.DTOs.Payment.PaymentRequestDto
                    {
                        Amount = dto.Total,
                        ReferenceData = order.OrderId.ToString(),
                        FirstName = dto.Customer.Name,
                        Email = dto.Customer.Email,
                        Phone = dto.Customer.Phone,
                        ProductInfo = $"Order #{order.OrderId}",
                        SuccessUrl = $"{Request.Scheme}://{Request.Host}/api/checkout/payu-callback?status=success",
                        FailureUrl = $"{Request.Scheme}://{Request.Host}/api/checkout/payu-callback?status=failure"
                    };

                    var payResponse = await _paymentService.ProcessPaymentAsync(pr);

                    return Ok(new
                    {
                        id = order.OrderId,
                        requiresAction = true,
                        paymentUrl = payResponse.RedirectUrl,
                        paymentData = payResponse.PaymentData
                    });
                }

                return Ok(new { id = order.OrderId, requiresAction = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateOrder failed");
                return StatusCode(500, "Failed to create order");
            }
        }

        [HttpPost("/api/checkout/payu-callback")]
        public async Task<IActionResult> PayUCallback([FromQuery] string status, [FromForm] IFormCollection form)
        {
            var txnid = form["txnid"].ToString();
            var resStatus = form["status"].ToString();
            var hash = form["hash"].ToString();

            if (string.IsNullOrEmpty(txnid)) return BadRequest("Invalid callback");

            // Look up order based on txnid logic : "PAYU-GUID"... Wait, we passed orderId as txnid? No, txnid is randomly generated in PaymentGatewayService!
            // Wait, how do we link it? ReferenceData was passed. Does PayU return udf or reference data?
            // Actually, in PaymentGatewayService, I generated txnid. But it's not saved to Order.
            // Let's lookup via simple db query on PaymentDetails or we must pass order_id in productinfo or pass it directly in txnid.
            // Let's modify txnid generation in Service to include OrderId! But that's complicated now.
            // Wait, we can extract it from productinfo which is "Order #ID"
            var productInfo = form["productinfo"].ToString() ?? "";
            var orderIdStr = productInfo.Replace("Order #", "");
            if (!int.TryParse(orderIdStr, out var orderId)) return BadRequest("Order not found");

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return NotFound("Order not found");

            if (status == "success" && resStatus == "success")
            {
                order.Status = DNASoftech.Domain.Enums.OrderStatus.Confirmed;
                order.PaymentDetails = $"PayU TxnId: {txnid}";
            }
            else
            {
                order.Status = DNASoftech.Domain.Enums.OrderStatus.Cancelled;
                order.PaymentDetails = $"PayU Failed: {txnid}";
            }

            await _db.SaveChangesAsync();

            // Redirect to Success or Failure UI page
            return Redirect($"/Checkout/Success?orderId={orderId}");
        }
    }

    // DTOs
    public class CreateOrderDto
    {
        public OrderItemDto[] Items { get; set; } = Array.Empty<OrderItemDto>();
        public CustomerDto? Customer { get; set; }
        public PaymentDto? Payment { get; set; }
        public decimal Total { get; set; }
    }

    public class OrderItemDto { public int ProductId { get; set; } public int Quantity { get; set; } public string? Name { get; set; } public decimal Price { get; set; } public string? ImageUrl { get; set; } }
    public class CustomerDto { public string? Name { get; set; } public string? Email { get; set; } public string? Phone { get; set; } public string? Address { get; set; } public string? City { get; set; } public string? Postal { get; set; } }
    public class PaymentDto { public string? Method { get; set; } public string? Details { get; set; } }
}
