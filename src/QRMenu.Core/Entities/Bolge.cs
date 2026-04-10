namespace QRMenu.Core.Entities
{
    public class Bolge
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int SiraNo { get; set; } = 0;

        public ICollection<Masa> Masalar { get; set; } = new List<Masa>();
    }
}
