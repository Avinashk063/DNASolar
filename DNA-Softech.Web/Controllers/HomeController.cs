using Microsoft.AspNetCore.Mvc;

namespace DNASoftech.Web.Controllers
{
    /// <summary>
    /// MVC controller for the home/landing page.
    /// Route: GET /
    /// </summary>
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
            return PhysicalFile(filePath, "text/html");
        }
    }
}
