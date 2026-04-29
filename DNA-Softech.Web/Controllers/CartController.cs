using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using DNASoftech.Application.DTOs.Cart;
using DNASoftech.Application.Interface;

namespace DNASoftech.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        public CartController(ICartService cartService) { _cartService = cartService; }

        private string? GetCurrentUserId() =>
            User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            try
            {
                var cart = await _cartService.GetCartAsync(userId);
                var result = cart.Items.Select(i => new { productId = i.ProductId, quantity = i.Quantity });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to load cart. Please try again." });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Upsert([FromBody] UpsertCartItemRequestDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (dto.ProductId <= 0) return BadRequest("Invalid product");

            try
            {
                dto.UserId = userId;
                await _cartService.AddOrUpdateItemAsync(dto);
                return Ok();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Failed to update cart. Please try again." });
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
                var cart = await _cartService.GetCartAsync(userId);
                if (cart.Items.All(i => i.ProductId != productId)) return NotFound();
                await _cartService.RemoveItemAsync(userId, productId);
                return Ok();
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Failed to remove item from cart. Please try again." });
            }
        }
    }
}
