namespace QRMenu.Core.Entities
{
    public class Masa
    {
        public int Id { get; set; }
        public int MasaNo { get; set; }
        public bool AktifMi { get; set; } = true;
        public string? QrKodUrl { get; set; }

        // Navigation Properties
        public ICollection<Siparis> Siparisler { get; set; } = new List<Siparis>();
        public ICollection<Oturum> Oturumlar { get; set; } = new List<Oturum>();
    }
}
