using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRMenu.Data.Data;
using System.Security.Claims;

namespace QRMenu.Web.Controllers
{
    public class AuthController : Controller
    {
        private readonly QRMenuDbContext _context;

        public AuthController(QRMenuDbContext context)
        {
            _context = context;
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
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == username && u.AktifMi);

            // BCrypt hash'li şifre kontrolü (bcrypt hash $2 ile başlar)
            // Geriye dönük uyumluluk: eski düz metin hashler için de kontrol yap
            bool sifreGecerli = false;
            if (user != null)
            {
                if (user.SifreHash.StartsWith("$2"))
                {
                    // BCrypt hash
                    sifreGecerli = BCrypt.Net.BCrypt.Verify(password, user.SifreHash);
                }
                else
                {
                    // Eski düz metin (geçiş dönemi) — geçerliyse BCrypt'e migrate et
                    sifreGecerli = user.SifreHash == password;
                    if (sifreGecerli)
                    {
                        user.SifreHash = BCrypt.Net.BCrypt.HashPassword(password);
                        await _context.SaveChangesAsync();
                    }
                }
            }

            if (user == null || !sifreGecerli)
            {
                ViewBag.Error = "Geçersiz kullanıcı adı veya şifre.";
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.KullaniciAdi),
                new Claim(ClaimTypes.Role, user.Rol.ToString()),
                new Claim("FullName", user.AdSoyad)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToRole(user.Rol.ToString());
        }

        [HttpGet("/Auth/Logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        private IActionResult RedirectToRole(string? role)
        {
            return role switch
            {
                "Admin" => RedirectToAction("Index", "Admin"),
                "Garson" => RedirectToAction("Masalar", "Garson"),
                "Kasa" => RedirectToAction("Masalar", "Kasa"),
                "Mutfak" => RedirectToAction("Panel", "Mutfak"),
                _ => RedirectToAction("Login")
            };
        }
    }
}
