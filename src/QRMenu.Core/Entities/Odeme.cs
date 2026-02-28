using QRMenu.Core.Enums;

namespace QRMenu.Core.Entities
{
    public class Odeme
    {
        public int Id { get; set; }
        public int SiparisId { get; set; }
        public decimal Tutar { get; set; }
        public OdemeTipi OdemeTipi { get; set; }
        public DateTime OdemeTarihi { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public Siparis Siparis { get; set; } = null!;
    }
}
