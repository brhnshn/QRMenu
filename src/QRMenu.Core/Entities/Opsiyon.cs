namespace QRMenu.Core.Entities
{
    public class Opsiyon
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? AdEN { get; set; }
        public string Grup { get; set; } = string.Empty; // "Boyut", "Süt Tipi"
        public decimal EkFiyat { get; set; }

        // Navigation Properties
        public ICollection<UrunOpsiyon> UrunOpsiyonlar { get; set; } = new List<UrunOpsiyon>();
    }
}
