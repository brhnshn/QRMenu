namespace QRMenu.Core.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string TabloAdi { get; set; } = string.Empty;
        public int KayitId { get; set; }
        public string Islem { get; set; } = string.Empty; // "INSERT", "UPDATE", "DELETE"
        public string? EskiDeger { get; set; }  // JSON
        public string? YeniDeger { get; set; }  // JSON
        public int? KullaniciId { get; set; }
        public DateTime IslemTarihi { get; set; } = DateTime.UtcNow;
    }
}
