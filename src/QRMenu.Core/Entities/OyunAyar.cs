using System.Collections.Generic;

namespace QRMenu.Core.Entities
{
    public class OyunAyar
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty; 
        public string Tip { get; set; } = string.Empty; // "CARKIFELEK", "HAFIZA", "KAZIKAZAN"
        public bool AktifMi { get; set; } = true;

        public ICollection<OyunOdul> Oduller { get; set; } = new List<OyunOdul>();
    }
}
