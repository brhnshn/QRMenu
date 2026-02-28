namespace QRMenu.Core.Entities
{
    public class Kategori
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? AdEN { get; set; }
        public int SiraNo { get; set; }
        public bool AktifMi { get; set; } = true;

        // Navigation Properties
        public ICollection<Urun> Urunler { get; set; } = new List<Urun>();
    }
}
