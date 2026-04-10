namespace QRMenu.Web.ViewModels
{
    public class BolgeFormViewModel
    {
        public string Ad { get; set; } = string.Empty;
        public int SiraNo { get; set; }
    }

    public class MasaFormViewModel
    {
        public int MasaNo { get; set; }
        public int? BolgeId { get; set; }
    }
}
