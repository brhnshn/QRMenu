namespace QRMenu.Core.Entities
{
    public class Sepet
    {
        public int Id { get; set; }
        public int OturumId { get; set; }
        public decimal ToplamTutar { get; set; }

        // Navigation Properties
        public Oturum Oturum { get; set; } = null!;
        public ICollection<SepetDetay> SepetDetaylar { get; set; } = new List<SepetDetay>();
    }
}
