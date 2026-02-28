using QRMenu.Core.Enums;

namespace QRMenu.Core.Entities
{
    public class SiparisDetay
    {
        public int Id { get; set; }
        public int SiparisId { get; set; }
        public int UrunId { get; set; }
        public int Adet { get; set; }
        public decimal BirimFiyat { get; set; }
        public string? SeciliOpsiyonlar { get; set; } // JSON string
        public SiparisDurum Durum { get; set; } = SiparisDurum.Onaylandi;

        // Navigation Properties
        public Siparis Siparis { get; set; } = null!;
        public Urun Urun { get; set; } = null!;
    }
}
