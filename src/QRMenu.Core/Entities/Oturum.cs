namespace QRMenu.Core.Entities
{
    public class Oturum
    {
        public int Id { get; set; }
        public int MasaId { get; set; }
        public string TokenHash { get; set; } = string.Empty; // SHA256 hash
        public bool Kullanildi { get; set; } = false;          // Tek kullanımlık kontrol (Replay Attack)
        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
        public DateTime SonIslemTarihi { get; set; } = DateTime.UtcNow; // Sliding expiration
        public bool AktifMi { get; set; } = true;

        // Navigation Properties
        public Masa Masa { get; set; } = null!;
        public Sepet? Sepet { get; set; }
    }
}
