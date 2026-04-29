using Microsoft.AspNetCore.Mvc;

namespace DNASoftech.Web.Controllers
{
    /// <summary>
    /// MVC controller for shop product listing and product detail pages.
    /// Routes:
    ///   GET /Shop              → product listing grid
    ///   GET /Shop/Product/{id} → product detail
    /// </summary>
    public class ShopController : Controller
    {
        /// <summary>
        /// Product listing page. Products are loaded client-side via fetch to /api/products.
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Product detail page. Product data is loaded client-side via fetch to /api/products/{id}.
        /// </summary>
        [Route("Shop/Product/{id:int}")]
        public IActionResult Product(int id)
        {
            // Pass id to the view via ViewData so the JS knows which product to fetch
            ViewData["ProductId"] = id;
            return View();
        }

        [Route("Shop/Orders")]
        public IActionResult Orders()
        {
            return View();
        }

        [Route("Shop/Checkout")]
        public IActionResult Checkout()
        {
            return View();
        }
    }
}
