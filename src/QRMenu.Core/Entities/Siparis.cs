using QRMenu.Core.Enums;

namespace QRMenu.Core.Entities
{
    public class Siparis
    {
        public int Id { get; set; }
        public int MasaId { get; set; }
        public int? OturumId { get; set; }
        public SiparisDurum Durum { get; set; } = SiparisDurum.Onaylandi;
        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
        public DateTime? GuncellemeTarihi { get; set; }
        public decimal ToplamTutar { get; set; }
        public string? Notlar { get; set; }

        // Concurrency kontrolü
        public byte[] RowVersion { get; set; } = null!;

        // Navigation Properties
        public Masa Masa { get; set; } = null!;
        public Oturum? Oturum { get; set; }
        public ICollection<SiparisDetay> SiparisDetaylar { get; set; } = new List<SiparisDetay>();
        public ICollection<Odeme> Odemeler { get; set; } = new List<Odeme>();
    }
}
