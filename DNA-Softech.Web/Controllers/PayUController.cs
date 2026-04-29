using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DNASoftech.Infrastructure.Data;
using DNASoftech.Domain.Models.ECommerce;
using DNASoftech.Domain.Enums;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Security.Claims;

namespace DNASoftech.Web.Controllers
{
    /// <summary>
    /// Handles PayU payment gateway integration.
    /// Routes: /api/payu/*
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PayUController : ControllerBase
    {
        private readonly DNASoftechDB _db;
        private readonly IConfiguration _config;
        private readonly ILogger<PayUController> _logger;

        private string MerchantKey => _config["Settings:PaymentGatewaySettings:MerchantKey"] ?? "gtKFFx";
        private string Salt => _config["Settings:PaymentGatewaySettings:Salt"] ?? "eCwWELxi";
        private string BaseUrl => _config["Settings:PaymentGatewaySettings:BaseUrl"] ?? "https://test.payu.in/_payment";

        public PayUController(DNASoftechDB db, IConfiguration config, ILogger<PayUController> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string Sha512(string input)
        {
            var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// Computes the PayU request hash.
        /// Format: sha512(key|txnid|amount|productinfo|firstname|email|udf1|udf2|udf3|udf4|udf5||||||salt)
        /// </summary>
        private string ComputeRequestHash(string txnid, string amount, string productinfo, string firstname, string email, string udf1 = "")
        {
            var raw = $"{MerchantKey}|{txnid}|{amount}|{productinfo}|{firstname}|{email}|{udf1}|||||||||{Salt}";
            return Sha512(raw);
        }

        /// <summary>
        /// Verifies the PayU response (reverse) hash.
        /// Format: sha512(salt|status||||||udf5|udf4|udf3|udf2|udf1|email|firstname|productinfo|amount|txnid|key)
        /// </summary>
        private bool VerifyResponseHash(string responseHash, string status, string txnid, string amount,
            string productinfo, string firstname, string email, string udf1 = "")
        {
            var raw = $"{Salt}|{status}|||||||||{udf1}|{email}|{firstname}|{productinfo}|{amount}|{txnid}|{MerchantKey}";
            var expected = Sha512(raw);
            return string.Equals(expected, responseHash, StringComparison.OrdinalIgnoreCase);
        }

        // ── POST /api/payu/initiate ───────────────────────────────────────────
        /// <summary>
        /// Creates a PendingConfirmation order and returns PayU form params + hash.
        /// Called from the checkout page JS before redirecting to PayU.
        /// </summary>
        [HttpPost("initiate")]
        public async Task<IActionResult> Initiate([FromBody] PayUInitiateRequest req)
        {
            if (req == null) return BadRequest("Missing payload");
            if (req.Items == null || req.Items.Length == 0) return BadRequest("Cart is empty");

            try
            {
                // Generate a unique transaction ID
                var txnid = $"DNA{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";

                // Format amount to 2 decimal places (PayU requirement)
                var amount = req.Amount.ToString("F2");
                var productinfo = "DNA Softech Solar Products";

                // Create the order as PendingConfirmation — will be Confirmed after PayU callback
                var order = new Order
                {
                    UserId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "guest",
                    ItemsJson = JsonSerializer.Serialize(req.Items),
                    CustomerName = req.Firstname + (string.IsNullOrWhiteSpace(req.Lastname) ? "" : " " + req.Lastname),
                    CustomerEmail = req.Email,
                    CustomerPhone = req.Phone ?? string.Empty,
                    CustomerAddress = string.Join(", ", new[] { req.Address, req.City, req.Postal }.Where(s => !string.IsNullOrWhiteSpace(s))),
                    TotalAmount = req.Amount,
                    PaymentMethod = PaymentMethod.PayU,
                    PaymentReference = txnid,
                    Status = OrderStatus.PendingConfirmation,
                    PaymentDetails = $"txnid:{txnid}|payu:1"
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                // Build absolute success/failure URLs
                var baseUri = $"{Request.Scheme}://{Request.Host}";
                var surl = $"{baseUri}/api/payu/success";
                var furl = $"{baseUri}/api/payu/failure";

                var hash = ComputeRequestHash(txnid, amount, productinfo, req.Firstname, req.Email, order.OrderId.ToString());

                _logger.LogInformation("PayU order initiated: txnid={Txnid} orderId={OrderId}", txnid, order.OrderId);

                return Ok(new
                {
                    key = MerchantKey,
                    txnid,
                    amount,
                    productinfo,
                    firstname = req.Firstname,
                    lastname = req.Lastname ?? "",
                    email = req.Email,
                    phone = req.Phone ?? "",
                    udf1 = order.OrderId.ToString(),
                    surl,
                    furl,
                    hash,
                    payuUrl = BaseUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayU initiate failed");
                return StatusCode(500, new { error = "Failed to initiate payment" });
            }
        }

        // ── POST /api/payu/success ────────────────────────────────────────────
        /// <summary>
        /// PayU redirects here (form POST) after a successful payment.
        /// Verifies hash, updates the order to Confirmed, redirects to orders page.
        /// </summary>
        [HttpPost("success")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Success()
        {
            try
            {
                var form = Request.Form;
                var status = form["status"].ToString();
                var txnid = form["txnid"].ToString();
                var amount = form["amount"].ToString();
                var product = form["productinfo"].ToString();
                var fname = form["firstname"].ToString();
                var email = form["email"].ToString();
                var udf1 = form["udf1"].ToString();
                var respHash = form["hash"].ToString();
                var mihpayid = form["mihpayid"].ToString();

                _logger.LogInformation("PayU callback: status={Status} txnid={Txnid} mihpayid={MihPayId}", status, txnid, mihpayid);

                // Verify hash
                if (!VerifyResponseHash(respHash, status, txnid, amount, product, fname, email, udf1))
                {
                    _logger.LogWarning("PayU hash mismatch for txnid={Txnid}", txnid);
                    return Redirect("/Shop/Checkout?payustatus=tampered");
                }

                if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("PayU non-success status={Status} txnid={Txnid}", status, txnid);
                    return Redirect("/Shop/Checkout?payustatus=failed");
                }

                // Find and update the order
                var order = await _db.Orders.FirstOrDefaultAsync(o => o.PaymentReference == txnid);
                if (order != null)
                {
                    order.Status = OrderStatus.Confirmed;
                    order.PaymentDetails = $"txnid:{txnid}|mihpayid:{mihpayid}|payu:1|status:success";
                    await _db.SaveChangesAsync();
                }

                return Redirect($"/Shop/Orders?payusuccess=1&orderId={order?.OrderId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayU success callback failed");
                return Redirect("/Shop/Orders?payustatus=error");
            }
        }

        // ── POST /api/payu/failure ────────────────────────────────────────────
        /// <summary>
        /// PayU redirects here (form POST) after a failed / cancelled payment.
        /// Updates the order to Cancelled, redirects back to checkout.
        /// </summary>
        [HttpPost("failure")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Failure()
        {
            try
            {
                var txnid = Request.Form["txnid"].ToString();
                var status = Request.Form["status"].ToString();

                _logger.LogWarning("PayU failure: status={Status} txnid={Txnid}", status, txnid);

                var order = await _db.Orders.FirstOrDefaultAsync(o => o.PaymentReference == txnid);
                if (order != null)
                {
                    order.Status = OrderStatus.Cancelled;
                    order.PaymentDetails = $"txnid:{txnid}|payu:1|status:{status}";
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayU failure callback processing error");
            }

            return Redirect("/Shop/Checkout?payustatus=failed");
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public class PayUInitiateRequest
    {
        public string Firstname { get; set; } = string.Empty;
        public string? Lastname { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Postal { get; set; }
        public decimal Amount { get; set; }
        public PayUCartItem[] Items { get; set; } = [];
    }

    public class PayUCartItem
    {
        public int ProductId { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
