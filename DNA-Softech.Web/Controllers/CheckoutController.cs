using Microsoft.AspNetCore.Mvc;

namespace DNASoftech.Web.Controllers
{
    /// <summary>
    /// MVC controller for the checkout flow.
    /// Routes:
    ///   GET /Checkout         → order form
    ///   GET /Checkout/Payment → payment selection / confirmation
    /// </summary>
    public class CheckoutController : Controller
    {
        /// <summary>Order details / shipping form.</summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>Payment method selection page.</summary>
        public IActionResult Payment()
        {
            return View();
        }

        public IActionResult Success(int orderId)
        {
            ViewData["OrderId"] = orderId;
            return View();
        }
    }
}
