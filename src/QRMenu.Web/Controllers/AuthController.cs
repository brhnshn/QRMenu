using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
                ViewBag.Error = "Geçersiz pin veya şifre.";
                return View();
            }

            if (user.Rol == QRMenu.Core.Enums.KullaniciRol.Admin)
            {
                ViewBag.Error = "Yönetici hesapları bu ekrandan giriş yapamaz. Lütfen yönetici ekranına gidin.";
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

            ViewBag.Error = "Geçersiz pin veya şifre.";
            return View();
        }

        [HttpGet("/Auth/AdminLogin")]
        public IActionResult AdminLogin()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                return RedirectToRole(role);
            }

            return View();
        }

        [HttpPost("/Auth/AdminLogin")]
        public async Task<IActionResult> AdminLogin(string username, string password)
        {
            var user = await _userManager.FindByNameAsync(username);

            if (user == null || !user.AktifMi)
            {
                ViewBag.Error = "Geçersiz kullanıcı adı veya şifre.";
                return View();
            }

            if (user.Rol != QRMenu.Core.Enums.KullaniciRol.Admin)
            {
                ViewBag.Error = "Bu ekrandan sadece sistem yöneticileri giriş yapabilir.";
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(user, password,
                isPersistent: true, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("Yönetici giriş yaptı. KullaniciAdi={Ad}", user.UserName);
                return RedirectToRole(user.Rol.ToString());
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Admin hesabı kilitlendi. KullaniciAdi={Ad}", username);
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
                "Barista" => RedirectToAction("Panel", "Mutfak"),
                "Mutfak" => RedirectToAction("Panel", "Mutfak"),
                _        => RedirectToAction("Login")
            };
        }

        // Gizli Kurtarma Ekranı (Hidden Entry)
        [HttpGet("/Auth/Recovery")]
        public IActionResult Recovery()
        {
            return View();
        }

        [HttpPost("/Auth/Recovery")]
        [EnableRateLimiting("RecoveryLimitPolicy")]
        public async Task<IActionResult> Recovery(string secretCode, string username, string password, [FromServices] IConfiguration config)
        {
            var expectedCode = config["Security:RecoveryCode"];
            if (string.IsNullOrEmpty(expectedCode) || secretCode != expectedCode)
            {
                ViewBag.Error = "Geçersiz güvenlik kodu.";
                return View();
            }

            var existing = await _userManager.FindByNameAsync(username);
            if (existing != null)
            {
                ViewBag.Error = "Bu kullanıcı ismi zaten mevcut.";
                return View();
            }

            var admin = new Kullanici
            {
                UserName = username,
                AdSoyad = "Sistem Kurtarıcısı",
                Rol = QRMenu.Core.Enums.KullaniciRol.Admin,
                AktifMi = true
            };

            var result = await _userManager.CreateAsync(admin, password);
            if (result.Succeeded)
            {
                // Assign role
                await _userManager.AddToRoleAsync(admin, "Admin");
                ViewBag.Success = "Yönetici kullanıcısı başarıyla oluşturuldu. Login ekranından giriş yapabilirsiniz.";
                return View();
            }

            ViewBag.Error = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }
    }
}
