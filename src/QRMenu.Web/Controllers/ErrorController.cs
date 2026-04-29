using Microsoft.AspNetCore.Mvc;

namespace QRMenu.Web.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode?}")]
        public IActionResult HttpStatusCodeHandler(int? statusCode)
        {
            if (statusCode.HasValue && statusCode.Value == 404)
            {
                return View("NotFound");
            }
            
            return View("Error");
        }
        
        [Route("Error")]
        public IActionResult Error()
        {
            return View("Error");
        }
    }
}
