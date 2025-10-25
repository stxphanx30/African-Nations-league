using Microsoft.AspNetCore.Mvc;

namespace African_Nations_league.Controllers
{
    public class LoadingController : Controller
    {
        public IActionResult Landing()
        {
            return View();
        }
        public IActionResult Loading2()
        {
            return View();
        }
    }
}
