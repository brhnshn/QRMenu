using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace QRMenu.Web.ViewModels
{
    // ===== KATEGORÄ° =====
    public class KategoriFormViewModel
    {
        [Required(ErrorMessage = "Kategori adÄ± zorunludur.")]
        [MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? AdEN { get; set; }

        public int SiraNo { get; set; }
    }

    // ===== ÃœRÃœN =====
    public class UrunFormViewModel
    {
        [Required(ErrorMessage = "ÃœrÃ¼n adÄ± zorunludur.")]
        [MaxLength(200)]
        public string Ad { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? AdEN { get; set; }

        [MaxLength(500)]
        public string? Aciklama { get; set; }

        [MaxLength(500)]
        public string? AciklamaEN { get; set; }

        [Required(ErrorMessage = "Fiyat zorunludur.")]
        [Range(0.01, 99999.99, ErrorMessage = "GeÃ§erli bir fiyat giriniz.")]
        public decimal Fiyat { get; set; }

        [Required(ErrorMessage = "Kategori seÃ§iniz.")]
        public int KategoriId { get; set; }

        public bool PopulerMi { get; set; }
        public bool AktifMi { get; set; } = true;

        public int? Kalori { get; set; }

        /// <summary>
        /// ÃœrÃ¼n gÃ¶rseli (max 2MB, jpg/png/webp)
        /// </summary>
        public IFormFile? Gorsel { get; set; }
    }

    // ===== OPSÄ°YON =====
    public class OpsiyonFormViewModel
    {
        [Required(ErrorMessage = "ÃœrÃ¼n ID zorunludur.")]
        public int UrunId { get; set; }

        [Required(ErrorMessage = "Opsiyon adÄ± zorunludur.")]
        [MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? AdEN { get; set; }

        [Required(ErrorMessage = "Grup adÄ± zorunludur.")]
        [MaxLength(50)]
        public string Grup { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? GrupEN { get; set; }

        [Range(0, 9999.99)]
        public decimal EkFiyat { get; set; }

        public bool Zorunlu { get; set; } = true;
    }

    // ===== HAPPY HOUR =====
    public class HappyHourFormViewModel
    {
        [Required(ErrorMessage = "BaÅŸlangÄ±Ã§ saati zorunludur.")]
        public string BaslangicSaati { get; set; } = "14:00"; // HH:mm

        [Required(ErrorMessage = "BitiÅŸ saati zorunludur.")]
        public string BitisSaati { get; set; } = "17:00"; // HH:mm

        [Range(1, 99, ErrorMessage = "Ä°ndirim oranÄ± 1-99 arasÄ±nda olmalÄ±dÄ±r.")]
        public decimal IndirimOrani { get; set; }

        public bool AktifMi { get; set; }

        public List<int>? UrunIds { get; set; }

        public int? UrunId { get; set; } // Geriye dÃ¶nÃ¼k uyumluluk
    }

    // ===== KULLANICI =====
    public class KullaniciFormViewModel
    {
        [Required(ErrorMessage = "KullanÄ±cÄ± adÄ± zorunludur.")]
        [MaxLength(50)]
        public string KullaniciAdi { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ad Soyad zorunludur.")]
        [MaxLength(100)]
        public string AdSoyad { get; set; } = string.Empty;

        [Required(ErrorMessage = "Åifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Åifre en az 6 karakter olmalÄ±dÄ±r.")]
        public string Sifre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rol zorunludur.")]
        public string Rol { get; set; } = "Garson";
    }

    public class KullaniciGuncelleViewModel
    {
        [Required(ErrorMessage = "KullanÄ±cÄ± adÄ± zorunludur.")]
        [MaxLength(50)]
        public string KullaniciAdi { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ad Soyad zorunludur.")]
        [MaxLength(100)]
        public string AdSoyad { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rol zorunludur.")]
        public string Rol { get; set; } = "Garson";

        public bool AktifMi { get; set; } = true;
    }

    public class SifreDegistirViewModel
    {
        [Required(ErrorMessage = "Åifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Åifre en az 6 karakter olmalÄ±dÄ±r.")]
        public string YeniSifre { get; set; } = string.Empty;
    }

    public class UrunTasiViewModel
    {
        public List<int> UrunIds { get; set; } = new();
        public int YeniKategoriId { get; set; }
    }
}
