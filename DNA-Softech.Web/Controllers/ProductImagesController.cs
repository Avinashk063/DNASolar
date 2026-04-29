using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DNASoftech.Infrastructure.Data;
using DNASoftech.Domain.Models.ECommerce;

namespace DNASoftech.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductImagesController : ControllerBase
    {
        private readonly DNASoftechDB _db;
        private readonly ILogger<ProductImagesController> _logger;

        public ProductImagesController(DNASoftechDB db, ILogger<ProductImagesController> logger)
        {
            _db = db; _logger = logger;
        }

        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetForProduct(int productId)
        {
            var imgs = await _db.ProductImages
                .Where(pi => pi.ProductId == productId)
                .OrderBy(pi => pi.SortOrder)
                .Select(pi => new
                {
                    id = pi.ProductImageId,
                    productId = pi.ProductId,
                    imageUrl = pi.ImageUrl,
                    hasData = pi.ImageData != null
                })
                .ToListAsync();
            return Ok(imgs);
        }

        [HttpPost("upload/{productId}")]
        public async Task<IActionResult> UploadImages(int productId)
        {
            if (!Request.HasFormContentType) return BadRequest("No form data");
            var form = await Request.ReadFormAsync();
            if (form.Files == null || form.Files.Count == 0) return BadRequest("No files");

            var list = new List<ProductImage>();
            foreach (var file in form.Files)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var data = ms.ToArray();
                list.Add(new ProductImage
                {
                    ProductId = productId,
                    ImageData = data,
                    ImageMimeType = file.ContentType,
                    ImageUrl = $"data:{file.ContentType};base64,{Convert.ToBase64String(data)}",
                    SortOrder = 0
                });
            }

            _db.ProductImages.AddRange(list);
            await _db.SaveChangesAsync();

            try
            {
                var prod = await _db.Products.FindAsync(productId);
                var first = list.FirstOrDefault();
                if (prod != null && first != null)
                {
                    prod.ImageUrl = first.ImageUrl;
                    prod.ImageMimeType = first.ImageMimeType;
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update product ImageUrl after upload for product {Id}", productId);
            }

            return Ok(new { added = list.Count });
        }
    }
}
