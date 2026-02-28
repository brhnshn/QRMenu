namespace QRMenu.Core.Entities
{
    public class SepetDetay
    {
        public int Id { get; set; }
        public int SepetId { get; set; }
        public int UrunId { get; set; }
        public int Adet { get; set; }
        public decimal BirimFiyat { get; set; }
        public string? SeciliOpsiyonlar { get; set; } // JSON string: [{"OpsiyonId":1,"Ad":"Büyük","EkFiyat":5.00}]

        // Navigation Properties
        public Sepet Sepet { get; set; } = null!;
        public Urun Urun { get; set; } = null!;
    }
}
