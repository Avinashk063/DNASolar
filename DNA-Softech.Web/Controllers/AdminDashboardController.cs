using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DNASoftech.Web.Controllers
{
    /// <summary>
    /// MVC controller for the Admin dashboard page (not the API controller).
    /// Route: GET /Admin — protected, Admin role only.
    /// The actual data is fetched client-side via the /api/admin/* JSON endpoints.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        [Route("Admin")]
        [Route("Admin/Index")]
        public IActionResult Index()
        {
            return View("~/Views/Admin/Index.cshtml");
        }
    }
}
