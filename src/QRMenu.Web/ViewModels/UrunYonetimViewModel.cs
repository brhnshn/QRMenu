using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace QRMenu.Web.ViewModels
{
    // ===== KATEGORİ =====
    public class KategoriFormViewModel
    {
        [Required(ErrorMessage = "Kategori adı zorunludur.")]
        [MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? AdEN { get; set; }

        public int SiraNo { get; set; }
    }

    // ===== ÜRÜN =====
    public class UrunFormViewModel
    {
        [Required(ErrorMessage = "Ürün adı zorunludur.")]
        [MaxLength(200)]
        public string Ad { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? AdEN { get; set; }

        [MaxLength(500)]
        public string? Aciklama { get; set; }

        [MaxLength(500)]
        public string? AciklamaEN { get; set; }

        [Required(ErrorMessage = "Fiyat zorunludur.")]
        [Range(0.01, 99999.99, ErrorMessage = "Geçerli bir fiyat giriniz.")]
        public decimal Fiyat { get; set; }

        [Required(ErrorMessage = "Kategori seçiniz.")]
        public int KategoriId { get; set; }

        public bool PopulerMi { get; set; }
        public bool AktifMi { get; set; } = true;

        /// <summary>
        /// Ürün görseli (max 2MB, jpg/png/webp)
        /// </summary>
        public IFormFile? Gorsel { get; set; }
    }

    // ===== OPSİYON =====
    public class OpsiyonFormViewModel
    {
        [Required(ErrorMessage = "Ürün ID zorunludur.")]
        public int UrunId { get; set; }

        [Required(ErrorMessage = "Opsiyon adı zorunludur.")]
        [MaxLength(100)]
        public string Ad { get; set; } = string.Empty;

        [Required(ErrorMessage = "Grup adı zorunludur.")]
        [MaxLength(50)]
        public string Grup { get; set; } = string.Empty;

        [Range(0, 9999.99)]
        public decimal EkFiyat { get; set; }
    }
}
