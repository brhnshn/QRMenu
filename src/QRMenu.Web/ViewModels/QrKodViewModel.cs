namespace QRMenu.Web.ViewModels
{
    public class QrKodViewModel
    {
        public int Id { get; set; }
        public int MasaNo { get; set; }
        public string QrUrl { get; set; } = "";
        public string QrBase64 { get; set; } = "";
        public bool DoluMu { get; set; }
        public int? BolgeId { get; set; }
        public string? BolgeAd { get; set; }
    }
}
