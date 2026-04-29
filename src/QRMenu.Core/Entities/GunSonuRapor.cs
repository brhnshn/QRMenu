namespace QRMenu.Core.Entities
{
    public class GunSonuRapor
    {
        public int Id { get; set; }
        public DateTime Tarih { get; set; }
        public decimal ToplamCiro { get; set; }
        public int SiparisSayisi { get; set; }
        public string OdemeTipleriJson { get; set; } = "[]";
        public DateTime KapanisTarihi { get; set; } = DateTime.UtcNow;
        public string? KapatanKullaniciId { get; set; }
    }
}
