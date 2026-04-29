using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DNASoftech.Infrastructure.Data;
using DNASoftech.Domain.Models.ECommerce;

namespace DNASoftech.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WishlistController : ControllerBase
    {
        private readonly DNASoftechDB _db;
        public WishlistController(DNASoftechDB db) { _db = db; }

        private string? GetCurrentUserId() =>
            User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            var items = await _db.WishlistItems
                .Where(w => w.UserId == userId)
                .Select(w => w.ProductId)
                .ToListAsync();
            return Ok(items);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Add([FromBody] int productId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            try
            {
                if (await _db.WishlistItems.AnyAsync(w => w.UserId == userId && w.ProductId == productId))
                    return BadRequest(new { error = "Already in wishlist" });
                _db.WishlistItems.Add(new WishlistItem { UserId = userId, ProductId = productId });
                await _db.SaveChangesAsync();
                return Ok();
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Failed to update wishlist. Please try again." });
            }
        }

        [HttpDelete("{productId}")]
        [Authorize]
        public async Task<IActionResult> Remove(int productId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            try
            {
                var wi = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);
                if (wi == null) return NotFound();
                _db.WishlistItems.Remove(wi);
                await _db.SaveChangesAsync();
                return Ok();
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Failed to update wishlist. Please try again." });
            }
        }
    }
}
