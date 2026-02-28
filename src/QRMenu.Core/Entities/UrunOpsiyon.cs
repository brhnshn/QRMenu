namespace QRMenu.Core.Entities
{
    public class UrunOpsiyon
    {
        public int UrunId { get; set; }
        public int OpsiyonId { get; set; }

        // Navigation Properties
        public Urun Urun { get; set; } = null!;
        public Opsiyon Opsiyon { get; set; } = null!;
    }
}
