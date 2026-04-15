using Microsoft.AspNetCore.Mvc;

namespace data_registry_local.Controllers
{
    public class InspectionsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
