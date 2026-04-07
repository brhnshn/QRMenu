namespace QRMenu.Core.Entities
{
    /// <summary>
    /// Happy Hour - Urun çoklu eşlemesi.
    /// </summary>
    public class HappyHourUrun
    {
        public int Id { get; set; }
        public int HappyHourId { get; set; }
        public int UrunId { get; set; }

        public HappyHour HappyHour { get; set; } = null!;
        public Urun Urun { get; set; } = null!;
    }
}
