using Microsoft.AspNetCore.Identity;
using QRMenu.Core.Enums;

namespace QRMenu.Core.Entities
{
    /// <summary>
    /// ASP.NET Identity tabanlı kullanıcı.
    /// IdentityUser'ın Id (string), UserName, PasswordHash, Email alanlarını miras alır.
    /// Özel alanlar: AdSoyad, Rol (enum), AktifMi.
    /// </summary>
    public class Kullanici : IdentityUser
    {
        public string AdSoyad { get; set; } = string.Empty;
        public KullaniciRol Rol { get; set; }
        public bool AktifMi { get; set; } = true;
    }
}
