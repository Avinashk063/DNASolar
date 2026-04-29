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
    /// API controller for admin operations: products, categories, users, orders.
    /// Route: /api/admin/*
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly DNASoftechDB _db;
        private readonly IEmailService _emailService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(DNASoftechDB db, IEmailService emailService, ILogger<AdminController> logger)
        {
            _db = db;
            _emailService = emailService;
            _logger = logger;
        }

        private static string ReadStatus(string? details)
        {
            if (string.IsNullOrWhiteSpace(details)) return "PendingConfirmation";
            var token = details.Split('|', StringSplitOptions.RemoveEmptyEntries)
                               .FirstOrDefault(p => p.TrimStart().StartsWith("status:", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(token)) return "PendingConfirmation";
            return token.Split(':', 2).LastOrDefault()?.Trim() ?? "PendingConfirmation";
        }

        private static string UpsertStatus(string? details, string status)
        {
            var source = details ?? string.Empty;
            var parts = source.Split('|', StringSplitOptions.RemoveEmptyEntries)
                              .Where(p => !p.TrimStart().StartsWith("status:", StringComparison.OrdinalIgnoreCase))
                              .ToList();
            parts.Add($"status:{status}");
            return string.Join('|', parts);
        }

        private bool IsAdminUser()
        {
            try
            {
                var isAuth = User?.Identity?.IsAuthenticated == true;
                var roles = User?.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();
                var email = User?.FindFirst(ClaimTypes.Email)?.Value ?? User?.Identity?.Name;
                _logger.LogDebug("IsAdminUser: auth={Auth} email={Email} roles={Roles}", isAuth, email, string.Join(',', roles));
                if (!isAuth) return false;
                return roles.Contains("Admin");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IsAdminUser check failed");
                return false;
            }
        }

        // ── Products ──────────────────────────────────────────────────────────

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            if (!IsAdminUser()) return Forbid();
            return Ok(await _db.Products.ToListAsync());
        }

        [RequestSizeLimit(10_000_000)]
        [HttpPost("products")]
        public async Task<IActionResult> CreateProduct()
        {
            try
            {
                _logger.LogDebug("CreateProduct: auth={Auth}", User?.Identity?.IsAuthenticated == true);
                Product p;
                if (Request.HasFormContentType)
                {
                    var form = await Request.ReadFormAsync();
                    p = new Product
                    {
                        Name = form["name"].ToString(),
                        Description = form["description"].ToString(),
                        Category = form["category"].ToString(),
                        ImageUrl = form["imageUrl"].ToString(),
                        InStock = true
                    };
                    if (decimal.TryParse(form["price"].ToString(), out var pr)) p.Price = pr;
                    if (decimal.TryParse(form["originalPrice"].ToString(), out var op)) p.OriginalPrice = op;
                    if (p.OriginalPrice.HasValue && p.OriginalPrice.Value > 0 && p.OriginalPrice.Value > p.Price)
                        p.Discount = (int)Math.Round(((p.OriginalPrice.Value - p.Price) / p.OriginalPrice.Value) * 100);

                    var file = form.Files.FirstOrDefault();
                    if (file != null)
                    {
                        using var ms = new MemoryStream();
                        await file.CopyToAsync(ms);
                        p.ImageData = ms.ToArray();
                        p.ImageMimeType = file.ContentType;
                        p.ImageUrl = $"data:{file.ContentType};base64,{Convert.ToBase64String(p.ImageData)}";
                    }
                }
                else
                {
                    p = await System.Text.Json.JsonSerializer.DeserializeAsync<Product>(Request.Body,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Product();
                    if (string.IsNullOrWhiteSpace(p.Name)) return BadRequest("Invalid payload");
                }

                if (string.IsNullOrWhiteSpace(p.Name)) return BadRequest("Product name is required");
                p.Description ??= string.Empty;
                p.Category = string.IsNullOrWhiteSpace(p.Category) ? "uncategorized" : p.Category.Trim();
                if (string.IsNullOrWhiteSpace(p.ImageUrl)) p.ImageUrl = "https://via.placeholder.com/500?text=No+Image";

                // Auto-create category if missing
                if (!string.IsNullOrWhiteSpace(p.Category))
                {
                    var catName = p.Category.Trim();
                    var existingCat = await _db.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == catName.ToLower());
                    if (existingCat == null)
                    {
                        existingCat = new Category { Name = catName };
                        _db.Categories.Add(existingCat);
                        await _db.SaveChangesAsync();
                    }
                    p.Category = existingCat.Name;
                }

                if (p.OriginalPrice.HasValue && p.OriginalPrice.Value > 0 && p.OriginalPrice.Value > p.Price)
                    p.Discount = (int)Math.Round(((p.OriginalPrice.Value - p.Price) / p.OriginalPrice.Value) * 100);

                _db.Products.Add(p);
                await _db.SaveChangesAsync();
                return Ok(p);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateProduct failed");
                return BadRequest($"Upload failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        [RequestSizeLimit(10_000_000)]
        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage()
        {
            if (!Request.HasFormContentType) return BadRequest("No form data");
            var form = await Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file == null) return BadRequest("No file");
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());
            return Ok(new { url = $"data:{file.ContentType};base64,{base64}", size = ms.Length });
        }

        [HttpPut("products/{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product p)
        {
            if (!IsAdminUser()) return Forbid();
            var ex = await _db.Products.FindAsync(id);
            if (ex == null) return NotFound();
            ex.Name = p.Name; ex.Description = p.Description; ex.Price = p.Price;
            ex.OriginalPrice = p.OriginalPrice; ex.Category = p.Category;
            ex.ImageUrl = p.ImageUrl; ex.InStock = p.InStock;
            ex.Discount = ex.OriginalPrice.HasValue && ex.OriginalPrice.Value > 0 && ex.OriginalPrice.Value > ex.Price
                ? (int)Math.Round(((ex.OriginalPrice.Value - ex.Price) / ex.OriginalPrice.Value) * 100) : 0;
            await _db.SaveChangesAsync();
            return Ok(ex);
        }

        [HttpDelete("products/{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var ex = await _db.Products.FindAsync(id);
            if (ex == null) return NotFound();
            _db.Products.Remove(ex);
            await _db.SaveChangesAsync();
            return Ok();
        }

        // ── Categories ────────────────────────────────────────────────────────

        [AllowAnonymous]
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories() =>
            Ok(await _db.Categories.ToListAsync());

        [AllowAnonymous]
        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] Category c)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Name)) return BadRequest("Missing name");
            if (await _db.Categories.AnyAsync(x => x.Name.ToLower() == c.Name.Trim().ToLower()))
                return Conflict("Category already exists");
            c.Name = c.Name.Trim();
            _db.Categories.Add(c);
            await _db.SaveChangesAsync();
            return Ok(c);
        }

        [HttpPut("categories/{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] Category c)
        {
            if (!IsAdminUser()) return Forbid();
            if (c == null || string.IsNullOrWhiteSpace(c.Name)) return BadRequest("Missing name");
            var ex = await _db.Categories.FindAsync(id);
            if (ex == null) return NotFound();

            var oldName = ex.Name;
            var newName = c.Name.Trim();

            if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                // Cascade rename to all products that carry the old category name
                var affected = await _db.Products
                    .Where(p => p.Category.ToLower() == oldName.ToLower())
                    .ToListAsync();
                foreach (var p in affected)
                    p.Category = newName;
            }

            ex.Name = newName;
            await _db.SaveChangesAsync();

            return Ok(ex);
        }

        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var ex = await _db.Categories.FindAsync(id);
            if (ex == null) return NotFound();
            if (await _db.Products.AnyAsync(p => p.Category.ToLower() == ex.Name.ToLower()))
                return BadRequest("Category contains products and cannot be deleted");
            _db.Categories.Remove(ex);
            await _db.SaveChangesAsync();
            return Ok();
        }

        // ── Users (AppUsers) ──────────────────────────────────────────────────

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            if (!IsAdminUser()) return Forbid();
            return Ok(await _db.AppUsers.Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.Role,
                u.Mobile,
                u.Address
            }).ToListAsync());
        }

        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var u = await _db.AppUsers.FindAsync(id);
            if (u == null) return NotFound();
            return Ok(new { u.Id, u.Name, u.Email, u.Role, u.Mobile, u.Address });
        }

        [HttpGet("users/{id}/orders-summary")]
        public async Task<IActionResult> GetUserOrdersSummary(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var user = await _db.AppUsers.FindAsync(id);
            if (user == null) return NotFound();
            var email = user.Email ?? string.Empty;
            if (string.IsNullOrWhiteSpace(email)) return BadRequest("User has no email");

            var orders = await _db.Orders
                .Where(o => o.CustomerEmail.ToLower() == email.ToLower())
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToListAsync();

            var productCounts = new Dictionary<int, int>();
            foreach (var o in orders)
            {
                if (string.IsNullOrWhiteSpace(o.ItemsJson)) continue;
                try
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<OrderItemDto>>(o.ItemsJson)
                                ?? new List<OrderItemDto>();
                    foreach (var it in items)
                    {
                        if (productCounts.ContainsKey(it.ProductId)) productCounts[it.ProductId] += it.Quantity;
                        else productCounts[it.ProductId] = it.Quantity;
                    }
                }
                catch { /* ignore malformed JSON */ }
            }

            var keys = productCounts.Keys.ToList();
            var prods = keys.Count > 0
                ? await _db.Products.Where(p => keys.Contains(p.ProductId)).ToListAsync()
                : new List<Product>();

            var productSummaries = prods.Select(p => new
            {
                ProductId = p.ProductId,
                Name = p.Name,
                UnitPrice = p.Price,
                Quantity = productCounts.GetValueOrDefault(p.ProductId),
                Subtotal = p.Price * productCounts.GetValueOrDefault(p.ProductId)
            }).ToList<object>();

            return Ok(new
            {
                User = new { user.Id, user.Name, user.Email },
                OrderCount = orders.Count,
                TotalSpent = orders.Sum(o => o.TotalAmount),
                Orders = orders.Select(o => new { o.OrderId, o.CreatedAtUtc, o.TotalAmount, o.PaymentMethod }).ToList(),
                Products = productSummaries
            });
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
        {
            if (!IsAdminUser()) return Forbid();
            var user = await _db.AppUsers.FindAsync(id);
            if (user == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(dto.Name)) user.Name = dto.Name.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Email)) user.Email = dto.Email.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(dto.Role)) user.Role = dto.Role;
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(dto.Password));
                user.PasswordHash = Convert.ToHexString(hash);
            }
            try
            {
                await _db.SaveChangesAsync();
                return Ok(new { user.Id, user.Name, user.Email, user.Role });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateUser failed");
                return StatusCode(500, "Failed to update user");
            }
        }

        // ── Orders ────────────────────────────────────────────────────────────

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders()
        {
            if (!IsAdminUser()) return Forbid();
            var orders = await _db.Orders.OrderByDescending(o => o.CreatedAtUtc).ToListAsync();
            return Ok(orders.Select(o => new
            {
                Id = o.OrderId,
                o.CustomerName,
                o.CustomerEmail,
                o.CustomerPhone,
                o.CustomerAddress,
                Total = o.TotalAmount,
                PaymentMethod = o.PaymentMethod.ToString(),
                o.PaymentDetails,
                CreatedAt = o.CreatedAtUtc,
                Status = o.Status.ToString()
            }));
        }

        [HttpPost("orders/{id}/confirm")]
        public async Task<IActionResult> ConfirmOrder(int id)
        {
            if (!IsAdminUser()) return Forbid();
            var order = await _db.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.PaymentDetails = UpsertStatus(order.PaymentDetails, "Confirmed");
            order.Status = DNASoftech.Domain.Enums.OrderStatus.Confirmed;
            await _db.SaveChangesAsync();

            try
            {
                var body = $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><title>Order Confirmation</title></head>
<body style=""margin:0;padding:0;background:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;"">
  <table width=""100%"" style=""max-width:600px;margin:40px auto;background:#fff;border-radius:12px;box-shadow:0 4px 15px rgba(0,0,0,.05);overflow:hidden;"">
    <tr><td style=""padding:32px 20px;background:#2874f0;text-align:center;"">
      <h1 style=""color:#fff;margin:0;font-size:24px;"">DNA SOFTECH SHOP</h1>
    </td></tr>
    <tr><td style=""padding:40px 32px;"">
      <div style=""text-align:center;margin-bottom:32px;"">
        <div style=""display:inline-block;background:#10b981;color:#fff;border-radius:50%;width:56px;height:56px;line-height:56px;font-size:28px;margin-bottom:16px;"">&#10003;</div>
        <h2 style=""margin:0;color:#0f172a;"">Order Confirmed!</h2>
        <p style=""color:#64748b;"">Hi there, thank you for shopping with us!</p>
      </div>
      <table width=""100%"" style=""background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;margin-bottom:32px;"">
        <tr><td style=""padding:24px;"">
          <h3 style=""margin:0 0 16px;color:#0f172a;border-bottom:1px solid #e2e8f0;padding-bottom:12px;"">Order Summary</h3>
          <table width=""100%"">
            <tr><td style=""color:#64748b;"">Order ID:</td><td align=""right"" style=""font-weight:600;"">#{order.OrderId}</td></tr>
            <tr><td style=""color:#64748b;"">Date:</td><td align=""right"">{DateTime.Now:dd MMM yyyy}</td></tr>
            <tr><td style=""font-weight:700;border-top:1px dashed #cbd5e1;padding-top:16px;"">Total:</td>
                <td align=""right"" style=""color:#2874f0;font-size:20px;font-weight:800;border-top:1px dashed #cbd5e1;padding-top:16px;"">&#8377;{order.TotalAmount:N2}</td></tr>
          </table>
        </td></tr>
      </table>
    </td></tr>
    <tr><td style=""background:#f8fafc;padding:24px;text-align:center;border-top:1px solid #e2e8f0;"">
      <p style=""color:#64748b;font-size:13px;margin:0;"">© {DateTime.Now.Year} DNA Softech Shop. All rights reserved.</p>
    </td></tr>
  </table>
</body></html>";
                await _emailService.SendEmailAsync(new[] { order.CustomerEmail }, $"Order #{order.OrderId} Confirmed", body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed sending confirmation email for order {Id}", order.OrderId);
            }

            return Ok(new { id = order.OrderId, status = "Confirmed" });
        }

        [AllowAnonymous]
        [HttpGet("check")]
        public IActionResult Check() =>
            User?.Identity?.IsAuthenticated == true && User.IsInRole("Admin") ? Ok() : Unauthorized();

        [HttpPost("promote-me")]
        public async Task<IActionResult> PromoteMe()
        {
            if (User?.Identity?.IsAuthenticated != true) return Unauthorized();
            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return BadRequest("No email claim");
            var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null) return NotFound("User not found");
            user.Role = "Admin";
            await _db.SaveChangesAsync();
            return Ok(new { promoted = true });
        }
    }
}

// ── Shared DTOs used across admin controller & order controller ───────────────
public record LoginDto(string Email, string Password);
public record CreateAdminDto(string Name, string Email, string Password);
public record UpdateUserDto(string? Name, string? Email, string? Password, string? Role);
