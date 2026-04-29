using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DNASoftech.Infrastructure.Data;

namespace DNASoftech.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly DNASoftechDB _db;
        public CategoriesController(DNASoftechDB db) { _db = db; }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var cats = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
                return Ok(cats);
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Failed to load categories. Please try again." });
            }
        }
    }
}
