using System.ComponentModel.DataAnnotations;

namespace QRMenu.Web.ViewModels
{
    public class BolgeFormViewModel
    {
        [Required]
        [MaxLength(100)]
        public string Ad { get; set; } = string.Empty;
        [Range(0, 9999)]
        public int SiraNo { get; set; }
    }

    public class MasaFormViewModel
    {
        [Range(1, 9999)]
        public int MasaNo { get; set; }
        [Range(1, int.MaxValue)]
        public int? BolgeId { get; set; }
    }
}
