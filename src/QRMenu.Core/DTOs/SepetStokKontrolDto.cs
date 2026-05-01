namespace QRMenu.Core.DTOs
{
    public class SepetStokKontrolDto
    {
        public bool GecerliMi => Sorunlar.Count == 0;
        public List<SepetStokSorunDto> Sorunlar { get; set; } = new();
    }

    public class SepetStokSorunDto
    {
        public int SepetDetayId { get; set; }
        public int UrunId { get; set; }
        public int? UrunVaryasyonId { get; set; }
        public string UrunAd { get; set; } = string.Empty;
        public int SepettekiAdet { get; set; }
        public int StokAdet { get; set; }
        public bool StokTukendi { get; set; }
        public string Mesaj { get; set; } = string.Empty;
    }
}
