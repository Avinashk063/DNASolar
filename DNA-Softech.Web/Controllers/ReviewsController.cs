using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DNASoftech.Infrastructure.Data;
using DNASoftech.Domain.Models.ECommerce;
using System.Security.Claims;

namespace DNASoftech.Web.Controllers
{
    [ApiController]
    [Route("api/products/{productId}/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly DNASoftechDB _db;
        public ReviewsController(DNASoftechDB db) { _db = db; }

        [HttpGet]
        public async Task<IActionResult> GetReviews(int productId)
        {
            var reviews = await _db.Reviews
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    rating = r.Rating,
                    text = r.Text,
                    verified = r.VerifiedPurchase,
                    date = r.CreatedAt,
                    mediaImageData = r.MediaImageData,
                    mediaVideoData = r.MediaVideoData
                })
                .ToListAsync();
            return Ok(reviews);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PostReview(int productId, [FromBody] ReviewCreateModel model)
        {
            if (model == null) return BadRequest("Invalid payload");
            if (model.Rating < 1 || model.Rating > 5 || string.IsNullOrWhiteSpace(model.Text))
                return BadRequest("Missing required fields");

            // Resolve reviewer name from authenticated user
            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;
            var appUser = string.IsNullOrEmpty(email)
                ? null
                : await _db.AppUsers.FirstOrDefaultAsync(u => u.Email == email.ToLower());
            var reviewerName = appUser?.Name ?? email ?? "Anonymous";

            var productExists = await _db.Products.AnyAsync(p => p.ProductId == productId);
            if (!productExists) return NotFound("Product not found");

            var review = new Review
            {
                ProductId = productId,
                Name = reviewerName,
                Rating = model.Rating,
                Text = model.Text.Trim(),
                VerifiedPurchase = model.VerifiedPurchase,
                MediaImageData = model.MediaImageData,
                MediaVideoData = model.MediaVideoData,
                CreatedAt = DateTime.UtcNow
            };

            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();

            // Keep Product.Rating and Product.Reviews in sync
            var product = await _db.Products.FindAsync(productId);
            if (product != null)
            {
                var allRatings = await _db.Reviews.Where(r => r.ProductId == productId).Select(r => r.Rating).ToListAsync();
                product.Rating = Math.Round(allRatings.Average(), 1);
                product.Reviews = allRatings.Count;
                await _db.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetReviews), new { productId },
                new { id = review.Id, name = review.Name, rating = review.Rating, text = review.Text, verified = review.VerifiedPurchase, date = review.CreatedAt });
        }
    }

    public class ReviewCreateModel
    {
        public int Rating { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool VerifiedPurchase { get; set; }
        public string? MediaImageData { get; set; }
        public string? MediaVideoData { get; set; }
    }
}
