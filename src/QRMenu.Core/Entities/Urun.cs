namespace QRMenu.Core.Entities
{
    public class Urun
    {
        public int Id { get; set; }
        public int KategoriId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? AdEN { get; set; }
        public string? Aciklama { get; set; }
        public string? AciklamaEN { get; set; }
        public decimal Fiyat { get; set; }
        public string? GorselUrl { get; set; }
        public bool AktifMi { get; set; } = true;
        public bool PopulerMi { get; set; } = false;
        public int SatisSayisi { get; set; } = 0;

        // Navigation Properties
        public Kategori Kategori { get; set; } = null!;
        public ICollection<UrunOpsiyon> UrunOpsiyonlar { get; set; } = new List<UrunOpsiyon>();
    }
}
