using Microsoft.AspNetCore.Mvc;

namespace CMCS_App.Controllers
{
    public class ClaimController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }

        public IActionResult Verify()
        {
            return View();
        }

        public IActionResult Approve()
        {
            return View();
        }
    }
}
