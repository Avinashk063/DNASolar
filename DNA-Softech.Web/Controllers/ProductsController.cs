using Microsoft.AspNetCore.Mvc;
using DNASoftech.Application.Interface;

namespace DNASoftech.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ILogger<ProductsController> _logger;
        private readonly IProductService _productService;

        public ProductsController(IProductService productService, ILogger<ProductsController> logger)
        {
            _productService = productService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var products = await _productService.GetAllAsync();
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET /api/products failed");
                return StatusCode(500, new { error = "Failed to load products. Please try again." });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOne(int id)
        {
            try
            {
                var product = await _productService.GetByIdAsync(id);

                if (product == null) return NotFound(new { error = "Product not found" });
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET /api/products/{Id} failed", id);
                return StatusCode(500, new { error = "Failed to load product details. Please try again." });
            }
        }
    }
}
