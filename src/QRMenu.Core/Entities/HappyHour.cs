namespace QRMenu.Core.Entities
{
    /// <summary>
    /// Happy Hour tanımı: Belirli saat aralığında otomatik indirim uygulanır.
    /// Tabloda tek bir kayıt olacak şekilde tasarlanmıştır (singleton).
    /// </summary>
    public class HappyHour
    {
        public int Id { get; set; }

        /// <summary>Başlangıç saati (örn: 14:00)</summary>
        public TimeSpan BaslangicSaati { get; set; }

        /// <summary>Bitiş saati (örn: 17:00)</summary>
        public TimeSpan BitisSaati { get; set; }

        /// <summary>İndirim oranı (0-100 arası yüzde, örn: 15 → %15 indirim)</summary>
        public decimal IndirimOrani { get; set; }

        /// <summary>Happy Hour aktif mi?</summary>
        public bool AktifMi { get; set; } = false;

        /// <summary>Son güncelleme zamanı (audit için)</summary>
        public DateTime GuncellemeTarihi { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Geriye dönük uyumluluk için tutulur.
        /// Yeni yapıda çoklu ürün desteği HappyHourUrunler üzerinden yönetilir.
        /// </summary>
        public int? UrunId { get; set; }

        public virtual Urun? Urun { get; set; }

        public ICollection<HappyHourUrun> HappyHourUrunler { get; set; } = new List<HappyHourUrun>();
    }
}
