using DNASoftech.Application.DTOs.Checkout;
using DNASoftech.Application.DTOs.Payment;
using DNASoftech.Application.Interface;
using DNASoftech.Domain.Enums;
using DNASoftech.Domain.Interfaces;
using DNASoftech.Domain.Models.ECommerce;
using System.Text;

namespace DNASoftech.Application.Service.ECommerce
{
    public class CheckoutService : ICheckoutService
    {
        private readonly ICartRepository _cartRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IPaymentService _paymentService;
        private readonly IEmailService _emailService;

        public CheckoutService(
            ICartRepository cartRepository,
            IOrderRepository orderRepository,
            IPaymentService paymentService,
            IEmailService emailService)
        {
            _cartRepository = cartRepository;
            _orderRepository = orderRepository;
            _paymentService = paymentService;
            _emailService = emailService;
        }

        public async Task<CheckoutResponseDto> CheckoutAsync(CheckoutRequestDto request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                throw new ArgumentException("UserId is required.");
            }

            var cart = await _cartRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (cart is null || cart.Items.Count == 0)
            {
                throw new InvalidOperationException("Cart is empty.");
            }

            var total = cart.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            if (total <= 0)
            {
                throw new InvalidOperationException("Cart total must be greater than zero.");
            }

            var paymentResult = await _paymentService.ProcessPaymentAsync(new PaymentRequestDto
            {
                Amount = total,
                Method = request.Payment.Method,
                ReferenceData = request.Payment.Details
            }, cancellationToken);

            var method = ParsePaymentMethod(request.Payment.Method);
            var order = new Order
            {
                UserId = request.UserId,
                CustomerName = request.Customer.Name,
                CustomerEmail = request.Customer.Email,
                CustomerPhone = request.Customer.Phone,
                CustomerAddress = request.Customer.Address,
                TotalAmount = total,
                PaymentMethod = method,
                PaymentReference = paymentResult.TransactionId ?? request.Payment.Details,
                PaymentStatus = paymentResult.Success ? PaymentStatus.Authorized : PaymentStatus.Failed,
                Status = paymentResult.Success ? OrderStatus.Confirmed : OrderStatus.PendingConfirmation,
                Items = cart.Items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.Product?.Price ?? 0m
                }).ToList()
            };

            await _orderRepository.AddAsync(order, cancellationToken);

            foreach (var item in cart.Items.ToList())
            {
                await _cartRepository.RemoveItemAsync(item, cancellationToken);
            }

            await _orderRepository.SaveChangesAsync(cancellationToken);

            await SendCheckoutEmailsAsync(order, cancellationToken);

            return new CheckoutResponseDto
            {
                OrderId = order.OrderId,
                OrderStatus = order.Status.ToString(),
                PaymentStatus = order.PaymentStatus.ToString(),
                PaymentReference = order.PaymentReference
            };
        }

        private async Task SendCheckoutEmailsAsync(Order order, CancellationToken cancellationToken)
        {
            var itemsHtml = string.Join("", order.Items.Select(i => $"<li>ProductId: {i.ProductId} - Qty: {i.Quantity}</li>"));
            var body = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>New Order Alert</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased;"">
    
    <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 12px; box-shadow: 0 4px 15px rgba(0,0,0,0.05); overflow: hidden;"">
        <tr>
            <td align=""center"" style=""padding: 24px 20px; background-color: #0f172a;"">
                <span style=""color: #94a3b8; font-size: 12px; font-weight: 700; letter-spacing: 1px; text-transform: uppercase;"">DNA Softech Shop • System Alert</span>
                <h1 style=""color: #ffffff; margin: 8px 0 0 0; font-size: 22px; font-weight: 700;"">New Order Received</h1>
            </td>
        </tr>
        <tr>
            <td style=""padding: 32px;"">
                <div style=""margin-bottom: 32px;"">
                    <h4 style=""margin: 0 0 12px 0; color: #0f172a; font-size: 14px; text-transform: uppercase; border-bottom: 2px solid #e2e8f0; padding-bottom: 8px;"">Order Items</h4>
                    <div style=""color: #334155; font-size: 14px; line-height: 1.6;"">
                        <ul style=""padding-left: 20px; margin: 0;"">
                            {itemsHtml}
                        </ul>
                    </div>
                </div>
            </td>
        </tr>
    </table>
</body>
</html>
";

            await _emailService.SendEmailAsync(new[] { order.CustomerEmail }, $"New Order #{order.OrderId} - Pending Confirmation", body, cancellationToken);
        }

        private static PaymentMethod ParsePaymentMethod(string? method)
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                return PaymentMethod.Unknown;
            }

            return method.Trim().ToLowerInvariant() switch
            {
                "cash" => PaymentMethod.Cash,
                "upi" => PaymentMethod.Upi,
                "card" => PaymentMethod.Card,
                "netbanking" => PaymentMethod.NetBanking,
                _ => PaymentMethod.Unknown
            };
        }
    }
}
