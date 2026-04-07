using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QRMenu.Core.Entities;
using System.Security.Claims;

namespace QRMenu.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly SignInManager<Kullanici> _signInManager;
        private readonly UserManager<Kullanici> _userManager;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            SignInManager<Kullanici> signInManager,
            UserManager<Kullanici> userManager,
            ILogger<AuthController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet("/Auth/Register")]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToRole(role);
            }
            return View();
        }

        [HttpPost("/Auth/Register")]
        public async Task<IActionResult> Register(string username, string password, string adSoyad, QRMenu.Core.Enums.KullaniciRol rol)
        {
            if (ModelState.IsValid)
            {
                var user = new Kullanici { UserName = username, AdSoyad = adSoyad, Rol = rol, AktifMi = true };
                var result = await _userManager.CreateAsync(user, password);
                
                if (result.Succeeded)
                {
                    // Rol ataması
                    await _userManager.AddToRoleAsync(user, rol.ToString());
                    
                    _logger.LogInformation("Yeni kullanıcı kayıt oldu: {KullaniciAdi}", username);
                    // Başarılı kayıttan sonra direkt giriş yap
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToRole(user.Rol.ToString());
                }
                
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                    ViewBag.Error = error.Description;
                }
            }
            return View();
        }

        [HttpGet("/Auth/Login")]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToRole(role);
            }

            return View();
        }

        [HttpPost("/Auth/Login")]
        public async Task<IActionResult> Login(string username, string password)
        {
            // UserName ile kullanıcıyı bul
            var user = await _userManager.FindByNameAsync(username);

            if (user == null || !user.AktifMi)
            {
                ViewBag.Error = "Geçersiz kullanıcı adı veya şifre.";
                return View();
            }

            // Identity SignInManager ile giriş yap (şifre hash doğrulaması otomatik)
            var result = await _signInManager.PasswordSignInAsync(user, password,
                isPersistent: true, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("Kullanıcı giriş yaptı. KullaniciAdi={Ad}, Rol={Rol}", user.UserName, user.Rol);
                return RedirectToRole(user.Rol.ToString());
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Hesap kilitlendi. KullaniciAdi={Ad}", username);
                ViewBag.Error = "Çok fazla hatalı giriş. Hesabınız 5 dakika kilitlendi.";
                return View();
            }

            ViewBag.Error = "Geçersiz kullanıcı adı veya şifre.";
            return View();
        }

        [HttpGet("/Auth/Logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        private IActionResult RedirectToRole(string? role)
        {
            return role switch
            {
                "Admin"  => RedirectToAction("Index", "Admin"),
                "Garson" => RedirectToAction("Masalar", "Garson"),
                "Kasa"   => RedirectToAction("Masalar", "Kasa"),
                "Mutfak" => RedirectToAction("Panel", "Mutfak"),
                _        => RedirectToAction("Login")
            };
        }
    }
}
