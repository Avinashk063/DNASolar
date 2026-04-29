using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DNASoftech.Application.DTOs.Payment;
using DNASoftech.Application.Interface;
using DNASoftech.Domain.Models.Settings;

namespace DNASoftech.Infrastructure.Services
{
    public class PaymentGatewayService : IPaymentService
    {
        private readonly PaymentGatewaySettings _settings;
        private readonly ILogger<PaymentGatewayService> _logger;

        public PaymentGatewayService(IOptions<PaymentGatewaySettings> settings, ILogger<PaymentGatewayService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public Task<PaymentResultDto> ProcessPaymentAsync(PaymentRequestDto request, CancellationToken cancellationToken = default)
        {
            if (request.Amount <= 0)
            {
                return Task.FromResult(new PaymentResultDto
                {
                    Success = false,
                    Status = "Failed",
                    Message = "Amount must be greater than zero."
                });
            }

            var transactionId = $"{_settings.Provider.ToUpperInvariant()}-{Guid.NewGuid():N}";
            
            if (_settings.Provider.Equals("PayU", StringComparison.OrdinalIgnoreCase))
            {
                // PayU Integration
                var key = _settings.MerchantKey ?? "";
                var salt = _settings.Salt ?? "";
                var amount = request.Amount.ToString("0.00");
                var productInfo = request.ProductInfo ?? "DNASoftech Order";
                var firstname = request.FirstName ?? "Customer";
                var email = request.Email ?? "customer@example.com";
                
                // Hash sequence: key|txnid|amount|productinfo|firstname|email|||||||||||salt
                var hashString = $"{key}|{transactionId}|{amount}|{productInfo}|{firstname}|{email}|||||||||||{salt}";
                var hash = ComputeSha512Hash(hashString);

                var paymentData = new Dictionary<string, string>
                {
                    { "key", key },
                    { "txnid", transactionId },
                    { "amount", amount },
                    { "productinfo", productInfo },
                    { "firstname", firstname },
                    { "email", email },
                    { "phone", request.Phone ?? "" },
                    { "surl", request.SuccessUrl ?? "" },
                    { "furl", request.FailureUrl ?? "" },
                    { "hash", hash }
                };

                return Task.FromResult(new PaymentResultDto
                {
                    Success = true,
                    Status = "Initiated",
                    TransactionId = transactionId,
                    Message = "PayU form generated.",
                    RedirectUrl = _settings.BaseUrl ?? "https://test.payu.in/_payment",
                    PaymentData = paymentData
                });
            }

            // Default Mock logic
            _logger.LogInformation("Processed payment via {Provider}. TransactionId={TransactionId}", _settings.Provider, transactionId);
            return Task.FromResult(new PaymentResultDto
            {
                Success = _settings.AutoApprovePayments,
                Status = _settings.AutoApprovePayments ? "Authorized" : "Pending",
                TransactionId = transactionId,
                Message = _settings.AutoApprovePayments ? "Payment authorized." : "Payment submitted for review."
            });
        }

        private string ComputeSha512Hash(string rawData)
        {
            using (var sha512 = System.Security.Cryptography.SHA512.Create())
            {
                var bytes = sha512.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}

