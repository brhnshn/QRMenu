namespace QRMenu.Core.Entities
{
    public class UrunGorsel
    {
        public int Id { get; set; }
        public int UrunId { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;

        // Navigation
        public Urun Urun { get; set; } = null!;
    }
}
