using QRMenu.Core.Enums;

namespace QRMenu.Core.Entities
{
    public class Kullanici
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string AdSoyad { get; set; } = string.Empty;
        public string SifreHash { get; set; } = string.Empty;
        public KullaniciRol Rol { get; set; }
        public bool AktifMi { get; set; } = true;
    }
}
