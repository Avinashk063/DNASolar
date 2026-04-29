using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DNASoftech.Infrastructure.Data;
using DNASoftech.Domain.Models.ECommerce;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using DNASoftech.Application.Interface;

namespace DNASoftech.Web.Controllers
{
    /// <summary>
    /// API controller for shop user authentication and profile management.
    /// Route: /api/users/*
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly DNASoftechDB _db;
        private readonly ILogger<UsersController> _logger;
        private readonly IEmailService _emailService;
        private readonly IMemoryCache _cache;

        public UsersController(DNASoftechDB db, ILogger<UsersController> logger, IEmailService emailService, IMemoryCache cache)
        {
            _db = db;
            _logger = logger;
            _emailService = emailService;
            _cache = cache;
        }

        // ── OTP ──────────────────────────────────────────────────────────────────

        [HttpPost("send-registration-otp")]
        public async Task<IActionResult> SendRegistrationOtp([FromBody] SendOtpDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Email))
                return BadRequest(new { error = "Email is required" });

            var email = dto.Email.Trim().ToLower();

            try
            {
                // Be resilient to transient DB/network failures when checking for existing user.
                const int maxAttempts = 3;
                int attempt = 0;
                AppUser? existing = null;
                while (attempt < maxAttempts)
                {
                    attempt++;
                    try
                    {
                        existing = await _db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);
                        break; // success
                    }
                    catch (System.Data.Common.DbException dbEx) when (attempt < maxAttempts)
                    {
                        _logger.LogWarning(dbEx, "Transient DB error while checking existing user for {Email} (attempt {Attempt}/{Max}). Retrying...", email, attempt, maxAttempts);
                        await Task.Delay(150 * attempt);
                        continue;
                    }
                    catch (IOException ioEx) when (attempt < maxAttempts)
                    {
                        _logger.LogWarning(ioEx, "IO error while checking existing user for {Email} (attempt {Attempt}/{Max}). Retrying...", email, attempt, maxAttempts);
                        await Task.Delay(150 * attempt);
                        continue;
                    }
                    catch (System.Net.Sockets.SocketException sockEx) when (attempt < maxAttempts)
                    {
                        _logger.LogWarning(sockEx, "Socket error while checking existing user for {Email} (attempt {Attempt}/{Max}). Retrying...", email, attempt, maxAttempts);
                        await Task.Delay(150 * attempt);
                        continue;
                    }
                }

                if (existing != null)
                    return BadRequest(new { error = "Email already registered" });

                var otp = Random.Shared.Next(100000, 999999).ToString();
                _cache.Set($"reg_otp:{email}", otp, TimeSpan.FromMinutes(10));

                await _emailService.SendEmailAsync(
                    new[] { dto.Email },
                    "Your DNA Softech Registration OTP",
                    $@"<div style='font-family:sans-serif;max-width:480px;margin:auto;padding:24px;'>
                        <h2 style='color:#4f46e5;margin-bottom:8px;'>Email Verification</h2>
                        <p style='color:#334155;'>Use the OTP below to complete your registration on <strong>DNA Softech</strong>.</p>
                        <div style='font-size:38px;font-weight:800;letter-spacing:10px;color:#0f172a;text-align:center;padding:24px 0;background:#f1f5f9;border-radius:12px;margin:24px 0;'>{otp}</div>
                        <p style='color:#64748b;font-size:13px;'>Valid for <strong>10 minutes</strong>. Never share this code with anyone.</p>
                    </div>");

                _logger.LogInformation("Registration OTP sent to {Email}", email);
                return Ok(new { sent = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send registration OTP to {Email}", email);
                return StatusCode(500, new { error = "Failed to send OTP. Please try again." });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password) || string.IsNullOrEmpty(dto.Name))
                return BadRequest(new { error = "Missing fields" });

            if (string.IsNullOrWhiteSpace(dto.Otp))
                return BadRequest(new { error = "OTP is required" });

            var email = dto.Email.Trim().ToLower();
            var cacheKey = $"reg_otp:{email}";

            if (!_cache.TryGetValue(cacheKey, out string? storedOtp) || storedOtp != dto.Otp.Trim())
                return BadRequest(new { error = "Invalid or expired OTP. Please request a new one." });

            try
            {
                var existing = await _db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);
                if (existing != null) return BadRequest(new { error = "Email already registered" });

                var user = new AppUser
                {
                    Name = dto.Name,
                    Email = email,
                    PasswordHash = HashPassword(dto.Password),
                    Role = "User"
                };

                _db.AppUsers.Add(user);
                await _db.SaveChangesAsync();
                _cache.Remove(cacheKey); // consume OTP

                // Sign the new user in immediately so [Authorize] works after registration
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, user.Role ?? "User")
                };
                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "CookieAuth"));
                var authProps = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7),
                    AllowRefresh = true
                };
                await HttpContext.SignInAsync("CookieAuth", principal, authProps);

                // Send welcome email to the newly registered user
                try
                {
                    var welcomeBody = $@"<!doctype html>
<html>
<body style='font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Arial;margin:0;padding:24px;background:#f8fafc;'>
  <div style='max-width:560px;margin:0 auto;background:#fff;padding:24px;border-radius:12px;box-shadow:0 8px 30px rgba(2,6,23,0.06)'>
    <h2 style='margin-top:0;color:#0f172a'>Welcome to DNA Softech, {System.Net.WebUtility.HtmlEncode(user.Name ?? user.Email)}!</h2>
    <p style='color:#475569'>Thanks for creating an account. We're excited to have you on board. You can now browse products, place orders and track them from your account.</p>
    <p style='color:#475569'>If you ever need help, reply to this email or visit our <a href='/' style='color:#2563eb'>support page</a>.</p>
    <p style='margin-top:18px;color:#64748b;font-size:13px'>Cheers,<br/>DNA Softech Team</p>
  </div>
</body>
</html>";

                    await _emailService.SendEmailAsync(new[] { user.Email }, "Welcome to DNA Softech", welcomeBody);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send welcome email to {Email}", user.Email);
                }

                return Ok(new { id = user.Id, name = user.Name, email = user.Email });
            }
            catch (System.Data.Common.DbException ex)
            {
                _logger.LogError(ex, "Database connection failed during registration for {Email}", email);
                return StatusCode(503, new { error = "Database is temporarily unavailable. Please try again in a moment." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration for {Email}", email);
                return StatusCode(500, new { error = "An unexpected error occurred. Please try again." });
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            if (User?.Identity?.IsAuthenticated != true) return Unauthorized();
            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            try
            {
                var u = await _db.AppUsers.FirstOrDefaultAsync(x => x.Email == email.ToLower());
                if (u == null) return NotFound();

                var result = new Dictionary<string, object?>
                {
                    ["id"] = u.Id,
                    ["name"] = u.Name,
                    ["email"] = u.Email,
                    ["mobile"] = u.Mobile,
                    ["address"] = u.Address,
                    ["addressLine1"] = u.AddressLine1,
                    ["addressLine2"] = u.AddressLine2,
                    ["city"] = u.City,
                    ["state"] = u.State,
                    ["zip"] = u.Zip,
                    ["country"] = u.Country
                };

                if (u.ProfileImageData != null && !string.IsNullOrEmpty(u.ProfileImageMimeType))
                    result["profilePhotoUrl"] = $"/api/users/profile-photo/{u.Id}";

                return Ok(result);
            }
            catch (System.Data.Common.DbException ex)
            {
                _logger.LogError(ex, "Database connection failed fetching profile for {Email}", email);
                return StatusCode(503, new { error = "Database is temporarily unavailable." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching profile for {Email}", email);
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            if (User?.Identity?.IsAuthenticated != true) return Unauthorized();
            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Unauthorized();
            var u = await _db.AppUsers.FirstOrDefaultAsync(x => x.Email == email.ToLower());
            if (u == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.Name)) u.Name = dto.Name;
            u.Mobile = dto.Mobile ?? u.Mobile;
            u.Address = dto.Address ?? u.Address;
            u.AddressLine1 = dto.AddressLine1 ?? u.AddressLine1;
            u.AddressLine2 = dto.AddressLine2 ?? u.AddressLine2;
            u.City = dto.City ?? u.City;
            u.State = dto.State ?? u.State;
            u.Zip = dto.Zip ?? u.Zip;
            u.Country = dto.Country ?? u.Country;

            try { await _db.SaveChangesAsync(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateProfile failed for {Email}", email);
                return StatusCode(500, new { error = "Failed to save profile: " + ex.Message });
            }

            return Ok(new { u.Id, u.Name, u.Email, u.Mobile, u.Address });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password))
                return BadRequest(new { error = "Missing credentials" });

            try
            {
                var found = await _db.AppUsers.FirstOrDefaultAsync(u => u.Email == dto.Email.ToLower());
                if (found == null || found.PasswordHash != HashPassword(dto.Password))
                    return BadRequest(new { error = "Invalid email or password" });

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, found.Email),
                    new Claim(ClaimTypes.Email, found.Email),
                    new Claim(ClaimTypes.NameIdentifier, found.Id.ToString()),
                    new Claim(ClaimTypes.Role, found.Role ?? "User")
                };

                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "CookieAuth"));
                var authProps = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7),
                    AllowRefresh = true
                };
                await HttpContext.SignInAsync("CookieAuth", principal, authProps);
                return Ok(new { id = found.Id, name = found.Name, email = found.Email });
            }
            catch (System.Data.Common.DbException ex)
            {
                _logger.LogError(ex, "Database connection failed during login for {Email}", dto.Email);
                return StatusCode(503, new { error = "Database is temporarily unavailable. Please try again in a moment." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for {Email}", dto.Email);
                return StatusCode(500, new { error = "An unexpected error occurred. Please try again." });
            }
        }

        [HttpPost("profile-photo")]
        public async Task<IActionResult> UploadProfilePhoto()
        {
            if (User?.Identity?.IsAuthenticated != true) return Unauthorized();
            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Unauthorized();
            var u = await _db.AppUsers.FirstOrDefaultAsync(x => x.Email == email.ToLower());
            if (u == null) return NotFound();
            if (!Request.HasFormContentType) return BadRequest("Expected multipart/form-data");
            var form = await Request.ReadFormAsync();
            var file = form.Files.GetFile("photo");
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            u.ProfileImageData = ms.ToArray();
            u.ProfileImageMimeType = file.ContentType ?? "application/octet-stream";
            try { await _db.SaveChangesAsync(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save profile photo for {Email}", email);
                return StatusCode(500, new { error = "Failed to save image: " + ex.Message });
            }
            return Ok(new { uploaded = true, profilePhotoUrl = $"/api/users/profile-photo/{u.Id}" });
        }

        [HttpGet("profile-photo/{id:int}")]
        public async Task<IActionResult> GetProfilePhoto(int id)
        {
            var u = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == id);
            if (u == null || u.ProfileImageData == null || string.IsNullOrEmpty(u.ProfileImageMimeType))
                return NotFound();
            return File(u.ProfileImageData, u.ProfileImageMimeType);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("CookieAuth");
            try
            {
                Response.Cookies.Delete("DNASession", new CookieOptions
                {
                    Path = "/",
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    HttpOnly = true
                });
            }
            catch { /* ignore */ }
            return Ok(new { loggedOut = true });
        }

        [HttpGet("debug")]
        public IActionResult Debug() => Ok(new
        {
            cookies = Request.Cookies.ToDictionary(k => k.Key, v => v.Value),
            isAuth = User?.Identity?.IsAuthenticated == true,
            claims = User?.Claims.Select(c => new { c.Type, c.Value }).ToList()
        });

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }
    }

    // DTOs
    public record RegisterDto(string Name, string Email, string Password, string? Otp);
    public record SendOtpDto(string Email);
    public record UpdateProfileDto(
        string? Name, string? Mobile, string? Address,
        string? AddressLine1, string? AddressLine2,
        string? City, string? State, string? Zip, string? Country);
}
