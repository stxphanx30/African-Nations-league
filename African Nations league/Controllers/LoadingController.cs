using Microsoft.AspNetCore.Mvc;

namespace African_Nations_league.Controllers
{
    public class LoadingController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
