using Microsoft.AspNetCore.Mvc;

namespace data_registry_local.Controllers
{
    public class IncedentsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
