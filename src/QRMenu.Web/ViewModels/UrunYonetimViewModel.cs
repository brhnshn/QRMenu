using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace QRMenu.Web.ViewModels
{
    // ===== KATEGORI =====
    public class KategoriFormViewModel
    {
        [Required(ErrorMessage = "Kategori adi zorunludur.")]
        [MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? AdEN { get; set; }

        public int SiraNo { get; set; }
    }

    // ===== URUN =====
    public class UrunFormViewModel
    {
        [Required(ErrorMessage = "Urun adi zorunludur.")]
        [MaxLength(200)]
        public string Ad { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? AdEN { get; set; }

        [MaxLength(500)]
        public string? Aciklama { get; set; }

        [MaxLength(500)]
        public string? AciklamaEN { get; set; }

        [Required(ErrorMessage = "Fiyat zorunludur.")]
        [Range(0.01, 99999.99, ErrorMessage = "Gecerli bir fiyat giriniz.")]
        public decimal Fiyat { get; set; }

        [Required(ErrorMessage = "Kategori seciniz.")]
        public int KategoriId { get; set; }

        public bool PopulerMi { get; set; }
        public bool AktifMi { get; set; } = true;

        public int? Kalori { get; set; }

        /// <summary>
        /// Urun gorseli (max 2MB, jpg/png/webp)
        /// </summary>
        public IFormFile? Gorsel { get; set; }
    }

    // ===== OPSIYON =====
    public class OpsiyonFormViewModel
    {
        [Required(ErrorMessage = "Urun ID zorunludur.")]
        public int UrunId { get; set; }

        [Required(ErrorMessage = "Opsiyon adi zorunludur.")]
        [MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? AdEN { get; set; }

        [Required(ErrorMessage = "Grup adi zorunludur.")]
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
        public string? BaslangicSaati { get; set; } // HH:mm

        public string? BitisSaati { get; set; } // HH:mm

        [Range(0, 99, ErrorMessage = "Indirim orani 0-99 arasinda olmalidir.")]
        public decimal IndirimOrani { get; set; }

        public bool AktifMi { get; set; }

        public List<int>? UrunIds { get; set; }

        public int? UrunId { get; set; } // Geriye donuk uyumluluk
    }

    // ===== KULLANICI =====
    public class KullaniciFormViewModel
    {
        [Required(ErrorMessage = "Kullanici adi zorunludur.")]
        [MaxLength(50)]
        public string KullaniciAdi { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ad Soyad zorunludur.")]
        [MaxLength(100)]
        public string AdSoyad { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Sifre en az 6 karakter olmalidir.")]
        public string Sifre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rol zorunludur.")]
        public string Rol { get; set; } = "Garson";
    }

    public class KullaniciGuncelleViewModel
    {
        [Required(ErrorMessage = "Kullanici adi zorunludur.")]
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
        [Required(ErrorMessage = "Sifre zorunludur.")]
        [MinLength(6, ErrorMessage = "Sifre en az 6 karakter olmalidir.")]
        public string YeniSifre { get; set; } = string.Empty;
    }

    public class UrunTasiViewModel
    {
        public List<int> UrunIds { get; set; } = new();
        public int YeniKategoriId { get; set; }
    }
}
