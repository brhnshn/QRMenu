namespace QRMenu.Core.Entities
{
    public class UrunVaryasyon
    {
        public int Id { get; set; }
        public int UrunId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? AdEN { get; set; }
        public decimal EkFiyat { get; set; }
        public int StokAdet { get; set; }
        public bool AktifMi { get; set; } = true;
        public bool AdminManuelPasifMi { get; set; }
        public int SiraNo { get; set; }

        public Urun Urun { get; set; } = null!;
    }
}
